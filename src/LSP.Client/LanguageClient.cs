﻿using Lsp.Capabilities.Client;
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
    using Processes;
    using Protocol;

    /// <summary>
    ///     A client for the Language Server Protocol.
    /// </summary>
    /// <remarks>
    ///     Note - at this stage, a <see cref="LanguageClient"/> cannot be reused once <see cref="Shutdown"/> has been called; instead, create a new one.
    /// </remarks>
    public sealed class LanguageClient
        : IDisposable
    {
        /// <summary>
        ///     The dispatcher for incoming requests, notifications, and responses.
        /// </summary>
        readonly ClientDispatcher _dispatcher = new ClientDispatcher();

        /// <summary>
        ///     The handler for dynamic registration of client
        /// </summary>
        readonly DynamicRegistrationHandler _dynamicRegistrationHandler = new DynamicRegistrationHandler();

        /// <summary>
        ///     The root application logger (used when creating sub-components).
        /// </summary>
        readonly ILogger _rootLogger;

        /// <summary>
        ///     The server process.
        /// </summary>
        ServerProcess _process;

        /// <summary>
        ///     The connection to the language server.
        /// </summary>
        ClientConnection _connection;

        /// <summary>
        ///     Completion source for language server readiness.
        /// </summary>
        TaskCompletionSource<object> _readyCompletion = new TaskCompletionSource<object>();

        /// <summary>
        ///     Create a new <see cref="LanguageClient"/>.
        /// </summary>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        /// <param name="serverStartInfo">
        ///     <see cref="ProcessStartInfo"/> used to start the server process.
        /// </param>
        public LanguageClient(ILogger logger, ProcessStartInfo serverStartInfo)
            : this(logger, new ExternalServerProcess(serverStartInfo))
        {
        }

        /// <summary>
        ///     Create a new <see cref="LanguageClient"/>.
        /// </summary>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        /// <param name="process">
        ///     <see cref="ServerProcess"/> used to start or connect to the server process.
        /// </param>
        public LanguageClient(ILogger logger, ServerProcess process)
            : this(logger)
        {
            if (process == null)
                throw new ArgumentNullException(nameof(process));

            _process = process;
            _process.Exited += ServerProcess_Exit;
        }

        /// <summary>
        ///     Create a new <see cref="LanguageClient"/>.
        /// </summary>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        LanguageClient(ILogger logger)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            _rootLogger = logger;
            Log = logger.ForContext<LanguageClient>();
            Workspace = new WorkspaceClient(this);
            Window = new WindowClient(this);
            TextDocument = new TextDocumentClient(this);

            _dispatcher.RegisterHandler(_dynamicRegistrationHandler);
        }

        /// <summary>
        ///     Dispose of resources being used by the client.
        /// </summary>
        public void Dispose()
        {
            ClientConnection connection = Interlocked.Exchange(ref _connection, null);
            connection?.Dispose();

            ServerProcess serverProcess = Interlocked.Exchange(ref _process, null);
            serverProcess?.Dispose();
        }

        /// <summary>
        ///     The client's logger.
        /// </summary>
        ILogger Log { get; }

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
        public ServerCapabilities ServerCapabilities => _dynamicRegistrationHandler.ServerCapabilities;

        /// <summary>
        ///     Has the language client been initialised?
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        ///     Is the connection to the language server open?
        /// </summary>
        public bool IsConnected => _connection != null && _connection.IsOpen;

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
                    _process?.HasExited ?? Task.CompletedTask
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
        /// <exception cref="InvalidOperationException">
        ///     <see cref="Initialize(string, CancellationToken)"/> has already been called.
        ///     
        ///     <see cref="Initialize(string, CancellationToken)"/> can only be called once per <see cref="LanguageClient"/>; if you have called <see cref="Shutdown"/>, you will need to use a new <see cref="LanguageClient"/>.
        /// </exception>
        public async Task Initialize(string workspaceRoot, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (IsInitialized)
                throw new InvalidOperationException("Client has already been initialised.");

            try
            {
                await Start();

                InitializeParams initializeParams = new InitializeParams
                {
                    RootPath = workspaceRoot,
                    Capabilities = ClientCapabilities,
                    ProcessId = Process.GetCurrentProcess().Id
                };

                Log.Verbose("Sending 'initialize' message to language server...");

                InitializeResult result = await SendRequest<InitializeResult>("initialize", initializeParams, cancellationToken).ConfigureAwait(false);
                if (result == null)
                    throw new LspException("Server replied to 'initialize' request with a null response.");

                _dynamicRegistrationHandler.ServerCapabilities = result.Capabilities;

                Log.Verbose("Sent 'initialize' message to language server.");

                Log.Verbose("Sending 'initialized' notification to language server...");

                SendEmptyNotification("initialized");

                Log.Verbose("Sent 'initialized' notification to language server.");

                IsInitialized = true;
                _readyCompletion.TrySetResult(null);
            }
            catch (Exception initializationError)
            {
                // Capture the initialisation error so anyone awaiting IsReady will also see it.
                _readyCompletion.TrySetException(initializationError);

                throw;
            }
        }

        /// <summary>
        ///     Stop the language server.
        /// </summary>
        /// <returns>
        ///     A <see cref="Task"/> representing the shutdown operation.
        /// </returns>
        public async Task Shutdown()
        {
            ClientConnection connection = _connection;
            if (connection != null)
            {
                if (connection.IsOpen)
                {
                    connection.SendEmptyNotification("shutdown");
                    connection.SendEmptyNotification("exit");
                    connection.Close(flushOutgoing: true);
                }

                await connection.HasClosed;
            }

            ServerProcess serverProcess = _process;
            if (serverProcess != null)
            {
                if (serverProcess.IsRunning)
                    await serverProcess.Stop();
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
        ///     Send an empty notification to the language server.
        /// </summary>
        /// <param name="method">
        ///     The notification method name.
        /// </param>
        public void SendEmptyNotification(string method)
        {
            ClientConnection connection = _connection;
            if (connection == null || !connection.IsOpen)
                throw new InvalidOperationException("Not connected to the language server.");

            connection.SendEmptyNotification(method);
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
            ClientConnection connection = _connection;
            if (connection == null || !connection.IsOpen)
                throw new InvalidOperationException("Not connected to the language server.");

            connection.SendNotification(method, notification);
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
            ClientConnection connection = _connection;
            if (connection == null || !connection.IsOpen)
                throw new InvalidOperationException("Not connected to the language server.");

            return connection.SendRequest(method, request, cancellationToken);
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
            ClientConnection connection = _connection;
            if (connection == null || !connection.IsOpen)
                throw new InvalidOperationException("Not connected to the language server.");

            return connection.SendRequest<TResponse>(method, request, cancellation);
        }

        /// <summary>
        ///     Start the language server.
        /// </summary>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        async Task Start()
        {
            if (_process == null)
                throw new ObjectDisposedException(GetType().Name);

            if (!_process.IsRunning)
            {
                Log.Verbose("Starting language server...");

                await _process.Start();

                Log.Verbose("Language server is running.");
            }

            Log.Verbose("Opening connection to language server...");

            if (_connection == null)
                _connection = new ClientConnection(_rootLogger, _dispatcher, input: _process.OutputStream, output: _process.InputStream);

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
        async void ServerProcess_Exit(object sender, EventArgs args)
        {
            Log.Verbose("Server process has exited; language client is shutting down...");

            ClientConnection connection = Interlocked.Exchange(ref _connection, null);
            if (connection != null)
            {
                using (connection)
                {
                    connection.Close();
                    await connection.HasClosed;
                }
            }

            await Shutdown();

            Log.Verbose("Language client shutdown complete.");
        }
    }
}
