# dotnet-language-client
.NET client for the Language Server Protocol (LSP)

## Usage

The `LanguageClient` class is still being built, so for now you'll need to use the `ClientConnection` class directly:

```csharp
// Note that if your language server is a native or netfx executable, you can replace "dotnet" with the full path to that executable.
ProcessStartInfo serverStartInfo = new ProcessStartInfo("dotnet")
{
    Arguments = @"""C:\MyLanguageServer\bin\Debug\netcoreapp2.0\MyLanguageServer.dll"" arg1 arg2 arg3", // Tell dotnet CLI to run the server
    CreateNoWindow = true,
    UseShellExecute = false,
    RedirectStandardInput = true,
    RedirectStandardOutput = true
};

Log.Information("Starting server...");
using (Process serverProcess = Process.Start(serverStartInfo))
{
    ClientConnection connection = new ClientConnection(
        input: serverProcess.StandardOutput.BaseStream,
        output: serverProcess.StandardInput.BaseStream,
        encoding: serverProcess.StandardInput.Encoding
    );

    connection.HandleNotification("dummy/notify", () =>
    {
        Log.Information("Received dummy notification from language server.");
    });

    connection.Start();

    Log.Information("Sending 'initialize' request...");
    InitializeResult result = await connection.SendRequest<InitializeResult>("initialize", new InitializeParams
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
    Log.Information("Received InitializeResult {@InitializeResult}...", result);

    Log.Information("Sending 'dummy' request...");
    await connection.SendRequest("dummy", new DummyParams
    {
        Message = "Hello, world!"
    });

    Log.Information("Sending 'shutdown' notification to language server...");
    connection.SendNotification("shutdown");

    Log.Information("Waiting for server to stop.");
    serverProcess.WaitForExit();
    Log.Information("Server stopped.");

    Log.Information("Shutting down connection...");
    connection.Stop();
    await connection.HasStopped;
    Log.Information("Connection has shut down.");
}
```