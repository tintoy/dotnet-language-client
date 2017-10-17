using Common;
using OmniSharp.Extensions.LanguageServer;
using Serilog;
using Serilog.Events;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using MSLogging = Microsoft.Extensions.Logging;

namespace Server
{
    /// <summary>
    ///     A simple language server called by the language client.
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

                Log.Information("All done, terminating...");
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
            Log.Information("Initialising language server...");

            LanguageServer languageServer = new LanguageServer(
                input: Console.OpenStandardInput(2048),
                output: Console.OpenStandardOutput(2048),
                loggerFactory: new MSLogging.LoggerFactory().AddSerilog(Log.Logger.ForContext<LanguageServer>())
            );
            languageServer.AddHandler(
                new ConfigurationHandler()
            );
            languageServer.AddHandler(
                new DummyHandler(languageServer)
            );

            Log.Information("Starting language server...");
            var initTask = languageServer.Initialize();

            languageServer.Shutdown += shutdownRequested =>
            {
                Log.Information("Language server shutdown (ShutDownRequested={ShutDownRequested}).", shutdownRequested);
            };

            Log.Information("Language server initialised; waiting for shutdown.");

            await initTask;

            Log.Information("Waiting for shutdown...");

            await languageServer.WasShutDown;
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
                    .Enrich.WithProperty("Source", "Server")
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
                    Path.ChangeExtension(logFile, ".Server" + logExtension)
                );

                loggerConfiguration = loggerConfiguration.WriteTo.File(logFile,
                    restrictedToMinimumLevel: logLevel
                );
            }

            Log.Logger = loggerConfiguration.CreateLogger();
        }
    }
}
