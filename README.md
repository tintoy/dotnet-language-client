# dotnet-language-client
.NET client for the Language Server Protocol (LSP)

## Usage

```csharp
ProcessStartInfo serverStartInfo = new ProcessStartInfo("dotnet")
{
    Arguments = $"\"{ServerAssembly}\" arg1 arg2 arg3",
    Environment =
    {
        ["SomeVar"] = "Foo"
    }
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
```