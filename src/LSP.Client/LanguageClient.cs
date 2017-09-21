using Lsp.Capabilities.Client;
using Lsp.Capabilities.Server;
using Lsp.Models;
using Serilog;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace LSP.Client
{
    using Clients;
    using Dispatcher;
    using Handlers;
    using Protocol;

    /// <summary>
    ///     A client for the Language Server Protocol.
    /// </summary>
    public sealed class LanguageClient
        : IDisposable
    {
        /// <summary>
        ///     The dispatcher for incoming requests, notifications, and responses.
        /// </summary>
        readonly ClientDispatcher _dispatcher = new ClientDispatcher();
        
        /// <summary>
        ///     <see cref="ProcessStartInfo"/> describing the server process.
        /// </summary>
        readonly ProcessStartInfo _serverStartInfo;

        /// <summary>
        ///     The server process.
        /// </summary>
        Process _serverProcess;

        /// <summary>
        ///     The connection to the language server.
        /// </summary>
        ClientConnection _connection;

        /// <summary>
        ///     Completion source for language server readiness.
        /// </summary>
        TaskCompletionSource<object> _readyCompletion = new TaskCompletionSource<object>();

        /// <summary>
        ///     The completion source used to wait for server exit.
        /// </summary>
        TaskCompletionSource<object> _serverExitCompletion;

        /// <summary>
        ///     Create a new <see cref="LanguageClient"/>.
        /// </summary>
        /// <param name="serverStartInfo">
        ///     <see cref="ProcessStartInfo"/> used to start the server process.
        /// </param>
        public LanguageClient(ProcessStartInfo serverStartInfo)
        {
            if (serverStartInfo == null)
                throw new ArgumentNullException(nameof(serverStartInfo));

            _serverStartInfo = serverStartInfo;
            Workspace = new WorkspaceClient(this);
            Window = new WindowClient(this);
            TextDocument = new TextDocumentClient(this);
        }

        /// <summary>
        ///     Dispose of resources being used by the client.
        /// </summary>
        public void Dispose()
        {
            _connection?.Dispose();

            if (_serverProcess != null)
            {
                if (!_serverProcess.HasExited)
                    _serverProcess.Kill();

                _serverProcess.Dispose();
                _serverProcess = null;
            }
        }

        /// <summary>
        ///     The LSP Text Document API.
        /// </summary>
        public TextDocumentClient TextDocument { get; }

        /// <summary>
        ///     The LSP Window API.
        /// </summary>
        public WindowClient Window { get; }

        /// <summary>
        ///     The LSP Workspace API.
        /// </summary>
        public WorkspaceClient Workspace { get; }

        /// <summary>
        ///     The client's capabilities.
        /// </summary>
        public ClientCapabilities ClientCapabilities { get; } = new ClientCapabilities
        {
            Workspace = new WorkspaceClientCapabilites
            {
                DidChangeConfiguration = new DidChangeConfigurationCapability
                {
                    DynamicRegistration = false
                }
            },
            TextDocument = new TextDocumentClientCapabilities
            {
                Synchronization = new SynchronizationCapability
                {
                    DidSave = true,
                    DynamicRegistration = false
                },
                Hover = new HoverCapability
                {
                    DynamicRegistration = false
                },
                Completion = new CompletionCapability
                {
                    CompletionItem = new CompletionItemCapability
                    {
                        SnippetSupport = false
                    },
                    DynamicRegistration = false
                }
            }
        };

        /// <summary>
        ///     The server's capabilities.
        /// </summary>
        public ServerCapabilities ServerCapabilities { get; private set; }

        /// <summary>
        ///     Has the language client been initialised?
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        ///     A <see cref="Task"/> that completes when the client is ready to handle requests.
        /// </summary>
        public Task IsReady => _readyCompletion.Task;

        /// <summary>
        ///     A <see cref="Task"/> that completes when the underlying connection has closed and the server has stopped.
        /// </summary>
        public Task HasShutdown
        {
            get
            {
                return Task.WhenAll(
                    _connection.HasClosed,
                    _serverExitCompletion?.Task ?? Task.CompletedTask
                );
            }
        }

        /// <summary>
        ///     Initialise the language server.
        /// </summary>
        /// <param name="workspaceRoot">
        ///     The workspace root.
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing initialisation.
        /// </returns>
        public async Task Initialize(string workspaceRoot, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (IsInitialized)
                throw new InvalidOperationException("Client has already been initialised.");

            Start();

            InitializeParams initializeParams = new InitializeParams
            {
                RootPath = workspaceRoot,
                Capabilities = ClientCapabilities,
                ProcessId = Process.GetCurrentProcess().Id
            };

            Trace.WriteLine("Sending 'initialize' message to language server...");

            InitializeResult result = await SendRequest<InitializeResult>("initialize", initializeParams, cancellationToken).ConfigureAwait(false);
            ServerCapabilities = result.Capabilities;

            Trace.WriteLine("Sent 'initialize' message to language server.");

            IsInitialized = true;
            _readyCompletion.SetResult(null);
        }
        
        /// <summary>
        ///     Stop the language server.
        /// </summary>
        /// <returns>
        ///     A <see cref="Task"/> representing the shutdown operation.
        /// </returns>
        public async Task Shutdown()
        {
            if (_connection != null)
            {
                if (_connection.IsOpen)
                {
                    _connection.SendNotification("shutdown");
                    _connection.Close(flushOutgoing: true);
                }

                await _connection.HasClosed;

                _connection = null;
            }

            if (_serverProcess != null)
            {
                if (!_serverProcess.HasExited)
                    _serverProcess.Kill();

                await _serverExitCompletion.Task;

                _serverProcess = null;
            }

            IsInitialized = false;
            _readyCompletion = new TaskCompletionSource<object>();
        }

        /// <summary>
        ///     Register a message handler.
        /// </summary>
        /// <param name="handler">
        ///     The message handler.
        /// </param>
        /// <returns>
        ///     An <see cref="IDisposable"/> representing the registration.
        /// </returns>
        public IDisposable RegisterHandler(IHandler handler) => _dispatcher.RegisterHandler(handler);

        /// <summary>
        ///     Send a notification to the language server.
        /// </summary>
        /// <param name="method">
        ///     The notification method name.
        /// </param>
        public void SendNotification(string method)
        {
            _connection.SendNotification(method);
        }

        /// <summary>
        ///     Send a notification message to the language server.
        /// </summary>
        /// <param name="method">
        ///     The notification method name.
        /// </param>
        /// <param name="notification">
        ///     The notification message.
        /// </param>
        public void SendNotification(string method, object notification)
        {
            _connection.SendNotification(method, notification);
        }

        /// <summary>
        ///     Send a request to the language server.
        /// </summary>
        /// <param name="method">
        ///     The request method name.
        /// </param>
        /// <param name="request">
        ///     The request message.
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional cancellation token that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the request.
        /// </returns>
        public Task SendRequest(string method, object request, CancellationToken cancellationToken = default(CancellationToken))
        {
            return _connection.SendRequest(method, request, cancellationToken);
        }

        /// <summary>
        ///     Send a request to the language server.
        /// </summary>
        /// <typeparam name="TResponse">
        ///     The response message type.
        /// </typeparam>
        /// <param name="method">
        ///     The request method name.
        /// </param>
        /// <param name="request">
        ///     The request message.
        /// </param>
        /// <param name="cancellation">
        ///     An optional cancellation token that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     A <see cref="Task{TResult}"/> representing the response.
        /// </returns>
        public Task<TResponse> SendRequest<TResponse>(string method, object request, CancellationToken cancellation = default(CancellationToken))
        {
            return _connection.SendRequest<TResponse>(method, request, cancellation);
        }

        /// <summary>
        ///     Start the language server.
        /// </summary>
        void Start()
        {
            Log.Verbose("Starting language server...");

            _serverStartInfo.CreateNoWindow = true;
            _serverStartInfo.UseShellExecute = false;
            _serverStartInfo.RedirectStandardInput = true;
            _serverStartInfo.RedirectStandardOutput = true;

            _serverExitCompletion = new TaskCompletionSource<object>();

            _serverProcess = Process.Start(_serverStartInfo);
            _serverProcess.EnableRaisingEvents = true;
            _serverProcess.Exited += ServerProcess_Exit;

            if (_serverProcess.HasExited)
                throw new InvalidOperationException($"Language server process terminated with exit code {_serverProcess.ExitCode}.");

            Log.Verbose("Language server is running.");

            Log.Verbose("Opening connection to language server...");

            _connection = new ClientConnection(_dispatcher, _serverProcess);
            _connection.Open();

            Log.Verbose("Connection to language server is open.");
        }

        /// <summary>
        ///     Called when the server process has exited.
        /// </summary>
        /// <param name="sender">
        ///     The event sender.
        /// </param>
        /// <param name="args">
        ///     The event arguments.
        /// </param>
        void ServerProcess_Exit(object sender, EventArgs args)
        {
            Log.Verbose("Server process has exited.");

            _serverExitCompletion?.TrySetResult(null);
        }
    }
}
