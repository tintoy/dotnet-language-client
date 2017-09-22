using Common;
using Lsp.Capabilities.Client;
using Lsp.Models;
using LSP.Client;
using Newtonsoft.Json.Linq;
using Serilog;
using Serilog.Events;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Client
{
    /// <summary>
    ///     A simple demo of using the <see cref="LanguageClient"/> to interact with a language server.
    /// </summary>
    static class Program
    {
        /// <summary>
        ///     The full path to the assembly that implements the language server.
        /// </summary>
        static readonly string ServerAssembly = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(typeof(Program).Assembly.Location), "..", "..", "..", "..",
            "Server/bin/Debug/netcoreapp2.0/Server.dll".Replace('/', Path.DirectorySeparatorChar)
        ));

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
            ProcessStartInfo serverStartInfo = new ProcessStartInfo(@"D:\Development\go\bin\go-langserver.exe")
            {
                Arguments = @"-mode stdio -logfile ""D:\Stage\go-langserver.log"""
            };

            Log.Information("Starting server...");
            LanguageClient client = new LanguageClient(serverStartInfo)
            {
                ClientCapabilities =
                {
                    TextDocument = new TextDocumentClientCapabilities
                    {
                        Hover = new HoverCapability
                        {
                            DynamicRegistration = false
                        },
                        Synchronization = new SynchronizationCapability
                        {
                            DidSave = true,
                            WillSave = false,
                            WillSaveWaitUntil = false,
                            DynamicRegistration = false
                        }
                    },
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
                    Log.Information("Language server says: [{MessageType}] {Message}", messageType, message);
                });

                await client.Initialize(workspaceRoot: @"D:\Development\go\src\github.com\sourcegraph\go-langserver");

                Log.Information("Client started.");

                Log.Information("Notifying language server of document open...");

                client.TextDocument.DidOpen(@"D:\Development\go\src\github.com\sourcegraph\go-langserver\main.go", "go");

                Log.Information("Asking language server for hover info...");

                Hover hover = await client.TextDocument.Hover(
                    @"D:\Development\go\src\github.com\sourcegraph\go-langserver\main.go",
                    line: 63,
                    column: 19
                );
                if (hover != null)
                    Log.Information("Got hover: {@Hover}", hover);
                else
                    Log.Information("No hover provided.");

                Log.Information("Stopping language server...");
                await client.Shutdown();
                Log.Information("Server stopped.");
            }
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
                    .Enrich.WithProperty("Source", "Client")
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
                    Path.ChangeExtension(logFile, ".Client" + logExtension)
                );

                loggerConfiguration = loggerConfiguration.WriteTo.File(logFile,
                    restrictedToMinimumLevel: logLevel
                );
            }

            Log.Logger = loggerConfiguration.CreateLogger();
        }
    }
}
