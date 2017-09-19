using Common;
using Lsp.Capabilities.Client;
using Lsp.Models;
using LSP.Client;
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

            Log.Logger =
                new LoggerConfiguration()
                    .MinimumLevel.Verbose()
                    .Enrich.WithProperty("Source", "Client")
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
            ProcessStartInfo serverStartInfo = new ProcessStartInfo("dotnet")
            {
                Arguments = $"\"{ServerAssembly}\""
            };

            Log.Information("Starting server...");
            using (LanguageClient client = new LanguageClient(serverStartInfo))
            {
                client.HandleNotification("dummy/notify", () =>
                {
                    Log.Information("Received dummy notification from language server.");
                });

                await client.Start();

                Log.Information("Client started.");

                Log.Information("Sending 'initialize' request...");
                InitializeResult initializeResult = await client.SendRequest<InitializeResult>("initialize", new InitializeParams
                {
                    RootPath = @"C:\Foo",
                    Capabilities = new ClientCapabilities
                    {
                        Workspace = new WorkspaceClientCapabilites
                        {

                        },
                        TextDocument = new TextDocumentClientCapabilities
                        {

                        }
                    }
                });
                Log.Information("Received InitializeResult {@InitializeResult}...", initializeResult);

                Log.Information("Sending 'dummy' request...");
                await client.SendRequest("dummy", new DummyParams
                {
                    Message = "Hello, world!"
                });

                Log.Information("Stopping language server...");
                await client.Stop();
                Log.Information("Server stopped.");
            }
        }
    }
}
