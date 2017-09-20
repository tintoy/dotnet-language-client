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

## Visual Studio Extension Sample

> What's with all the assemblies in the project folder?

VS won't find our assembly dependencies (a bigger issue for .NET Standard assemblies) unless we include them in the VSIX and provide a custom code-base (see `AssemblyDependencies.cs`).
There's a custom target in the project to include some of the, but I haven't had time to include the others (e.g. `Serilog` and friends).

I haven't had time to reorganise the assembly dependency stuff yet, but there's probably a much cleaner way to do it.
