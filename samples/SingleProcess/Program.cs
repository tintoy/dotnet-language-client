using Common;
using OmniSharp.Extensions.LanguageServer;
using OmniSharp.Extensions.LanguageServer.Capabilities.Client;
using LSP.Client;
using LSP.Client.Dispatcher;
using LSP.Client.Processes;
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
using MSLogging = Microsoft.Extensions.Logging;

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
            using (NamedPipeServerProcess serverProcess = new NamedPipeServerProcess("single-process-sample", Log.Logger))
            {
                await serverProcess.Start();

                Task clientTask = RunLanguageClient(serverProcess);
                Task serverTask = RunLanguageServer(input: serverProcess.ClientOutputStream, output: serverProcess.ClientInputStream);

                await Task.WhenAll(clientTask, serverTask);
            }
        }

        /// <summary>
        ///     Run a language client over the specified streams.
        /// </summary>
        /// <param name="serverProcess">
        ///     The <see cref="NamedPipeServerProcess"/> used to wire up the client and server streams.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        static async Task RunLanguageClient(NamedPipeServerProcess serverProcess)
        {
            if (serverProcess == null)
                throw new ArgumentNullException(nameof(serverProcess));
            
            Log.Information("Starting client...");
            LanguageClient client = new LanguageClient(Log.Logger, serverProcess)
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

                JObject settings = new JObject(
                    new JProperty("setting1", true),
                    new JProperty("setting2", "Hello")
                );

                await client.Initialize(
                    workspaceRoot: @"C:\Foo",
                    initializationOptions: settings
                );

                Log.Information("Client started.");

                // Update server configuration.
                client.Workspace.DidChangeConfiguration(settings);

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

            LanguageServer languageServer = new LanguageServer(input, output,
                loggerFactory: new MSLogging.LoggerFactory().AddSerilog(Log.Logger.ForContext<LanguageServer>())
            );

            languageServer.AddHandler(
                new ConfigurationHandler()
            );
            languageServer.AddHandler(
                new DummyHandler(languageServer)
            );

            languageServer.OnInitialize(parameters =>
            {
                JToken options = parameters.InitializationOptions as JToken;
                Log.Information("Server received initialisation options: {Options}", options?.ToString(Newtonsoft.Json.Formatting.None));

                return Task.CompletedTask;
            });

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
