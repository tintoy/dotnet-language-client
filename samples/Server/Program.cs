using Common;
using Lsp;
using Serilog;
using Serilog.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

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

            Log.Logger =
                new LoggerConfiguration()
                    .MinimumLevel.Verbose()
                    .Enrich.WithProperty("Source", "Server")
                    .WriteTo.Debug(
                        restrictedToMinimumLevel: LogEventLevel.Information
                    )
                    .WriteTo.Seq("http://localhost:5341/",
                        apiKey: Environment.GetEnvironmentVariable("LSP_SEQ_API_KEY"),
                        restrictedToMinimumLevel: LogEventLevel.Information
                    )
                    .CreateLogger();

            try
            {
                AsyncMain().Wait();

                Log.Information("All done, terminating...");
            }
            catch (AggregateException unexpectedError)
            {
                foreach (Exception exception in unexpectedError.Flatten().InnerExceptions)
                    Log.Error(unexpectedError, "Unexpected error.");
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

            LanguageServer languageServer = new LanguageServer(input: Console.OpenStandardInput(2048), output: Console.OpenStandardOutput(2048));
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
    }
}
