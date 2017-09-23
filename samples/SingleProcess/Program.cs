﻿using Common;
using Lsp;
using Lsp.Capabilities.Client;
using LSP.Client;
using LSP.Client.Dispatcher;
using LSP.Client.Protocol;
using Newtonsoft.Json.Linq;
using Serilog;
using Serilog.Events;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

using LanguageClient = LSP.Client.LanguageClient;

namespace SingleProcess
{
    /// <summary>
    ///     A language client and server in a single process, connected via anonymous pipes.
    /// </summary>
    static class Program
    {
        /// <summary>
        ///     The main program entry-point.
        /// </summary>
        static void Main()
        {
            SynchronizationContext.SetSynchronizationContext(
                new SynchronizationContext()
            );

            ConfigureLogging();

            try
            {
                AsyncMain().Wait();

                Log.Information("All done.");
            }
            catch (AggregateException unexpectedError)
            {
                foreach (Exception exception in unexpectedError.Flatten().InnerExceptions)
                    Log.Error(exception, "Unexpected error.");
            }
            catch (Exception unexpectedError)
            {
                Log.Error(unexpectedError, "Unexpected error.");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        /// <summary>
        ///     The main asynchronous program entry-point.
        /// </summary>
        /// <returns>
        ///     A <see cref="Task"/> representing program operation.
        /// </returns>
        static async Task AsyncMain()
        {
            using (AnonymousPipeServerStream serverInputStream = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None, bufferSize: 1024))
            using (AnonymousPipeServerStream serverOutputStream = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None, bufferSize: 1024))
            using (AnonymousPipeClientStream clientInputStream = new AnonymousPipeClientStream(PipeDirection.In, serverOutputStream.ClientSafePipeHandle))
            using (AnonymousPipeClientStream clientOutputStream = new AnonymousPipeClientStream(PipeDirection.Out, serverInputStream.ClientSafePipeHandle))
            {
                Task clientTask = RunLanguageClient(clientInputStream, clientOutputStream);
                Task serverTask = RunLanguageServer(serverInputStream, serverOutputStream);

                await clientTask;
                await serverTask;
            }
        }

        /// <summary>
        ///     Run a language client over the specified streams.
        /// </summary>
        /// <param name="input">
        ///     The input stream.
        /// </param>
        /// <param name="output">
        ///     The output stream.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        static async Task RunLanguageClient(Stream input, Stream output)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            if (output == null)
                throw new ArgumentNullException(nameof(output));

            Log.Information("Starting client...");
            ClientConnection connection = new ClientConnection(new ClientDispatcher(), input, output);
            LanguageClient client = new LanguageClient(connection)
            {
                ClientCapabilities =
                {
                    Workspace =
                    {
                        DidChangeConfiguration = new DidChangeConfigurationCapability
                        {
                            DynamicRegistration = false
                        }
                    }
                }
            };
            using (client)
            {
                // Listen for log messages from the language server.
                client.Window.OnLogMessage((message, messageType) =>
                {
                    Log.Information("Language server says: [{MessageType:l}] {Message}", messageType, message);
                });

                // Listen for our custom notification from the language server.
                client.HandleNotification<DummyParams>("dummy/notify", notification =>
                {
                    Log.Information("Received dummy notification from language server: {Message}",
                        notification.Message
                    );
                });

                await client.Initialize(workspaceRoot: @"C:\Foo");

                Log.Information("Client started.");

                // Update server configuration.
                client.Workspace.DidChangeConfiguration(
                    new JObject(
                        new JProperty("setting1", true),
                        new JProperty("setting2", "Hello")
                    )
                );

                // Invoke our custom handler.
                await client.SendRequest("dummy", new DummyParams
                {
                    Message = "Hello, world!"
                });

                Log.Information("Shutting down language client...");
                await client.Shutdown();
                Log.Information("Language client has shut down.");
            }
        }

        /// <summary>
        ///     Run a language server over the specified streams.
        /// </summary>
        /// <param name="input">
        ///     The input stream.
        /// </param>
        /// <param name="output">
        ///     The output stream.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        static async Task RunLanguageServer(Stream input, Stream output)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            if (output == null)
                throw new ArgumentNullException(nameof(output));

            Log.Information("Initialising language server...");

            LanguageServer languageServer = new LanguageServer(input, output);
            Task serverWasShutDown = languageServer.WasShutDown;

            languageServer.AddHandler(
                new ConfigurationHandler()
            );
            languageServer.AddHandler(
                new DummyHandler(languageServer)
            );

            Log.Information("Starting language server...");
            languageServer.Shutdown += shutdownRequested =>
            {
                Log.Information("Language server shutdown (ShutDownRequested={ShutDownRequested}).", shutdownRequested);
            };
            languageServer.Exit += exitCode =>
            {
                Log.Information("Language server exit (ExitCode={ExitCode}).", exitCode);
            };

            await languageServer.Initialize();

            Log.Information("Language server initialised; waiting for shutdown...");

            await Task.Delay(TimeSpan.FromSeconds(5));

            await serverWasShutDown;

            Log.Information("Language server has shut down.");
        }

        /// <summary>
        ///     Configure the global logger.
        /// </summary>
        static void ConfigureLogging()
        {
            LogEventLevel logLevel =
                Environment.GetEnvironmentVariable("LSP_VERBOSE_LOGGING") == "1"
                    ? LogEventLevel.Verbose
                    : LogEventLevel.Information;

            LoggerConfiguration loggerConfiguration =
                new LoggerConfiguration()
                    .MinimumLevel.Verbose()
                    .Enrich.WithProperty("ProcessId", Process.GetCurrentProcess().Id)
                    .Enrich.WithProperty("Source", "SingleProcess")
                    .WriteTo.Debug(
                        restrictedToMinimumLevel: logLevel
                    );

            string seqUrl = Environment.GetEnvironmentVariable("LSP_SEQ_URL");
            if (!String.IsNullOrWhiteSpace(seqUrl))
            {
                loggerConfiguration = loggerConfiguration.WriteTo.Seq(seqUrl,
                    apiKey: Environment.GetEnvironmentVariable("LSP_SEQ_API_KEY"),
                    restrictedToMinimumLevel: logLevel
                );
            }

            string logFile = Environment.GetEnvironmentVariable("LSP_LOG_FILE");
            if (!String.IsNullOrWhiteSpace(logFile))
            {
                string logExtension = Path.GetExtension(logFile);
                logFile = Path.GetFullPath(
                    Path.ChangeExtension(logFile, ".SingleProcess" + logExtension)
                );

                loggerConfiguration = loggerConfiguration.WriteTo.File(logFile,
                    restrictedToMinimumLevel: logLevel
                );
            }

            Log.Logger = loggerConfiguration.CreateLogger();
        }
    }
}