﻿using Lsp.Capabilities.Server;
using Lsp.Models;
using Serilog;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace LSP.Client
{
    using ClientApis;
    using Dispatcher;
    using Handlers;
    using Protocol;

    /// <summary>
    ///     A client for the Language Server Protocol.
    /// </summary>
    public sealed class LanguageClient
        : IDisposable
    {
        readonly ClientDispatcher _dispatcher = new ClientDispatcher();
        
        readonly ProcessStartInfo _serverStartInfo;

        /// <summary>
        ///     The server process.
        /// </summary>
        Process _serverProcess;

        /// <summary>
        ///     The connection to the language server.
        /// </summary>
        ClientConnection _connection;

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
        ///     The workspace API client.
        /// </summary>
        public WorkspaceClient Workspace { get; }

        /// <summary>
        ///     The server's capabilities.
        /// </summary>
        public ServerCapabilities ServerCapabilities { get; private set; }

        /// <summary>
        ///     A <see cref="Task"/> that completes when the client is ready to handle requests.
        /// </summary>
        public Task IsReady => _readyCompletion.Task;

        /// <summary>
        ///     A <see cref="Task"/> that completes when the underlying connection has closed and the server has stopped.
        /// </summary>
        public Task HasStopped
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
        ///     Start the language server.
        /// </summary>
        /// <returns>
        ///     A <see cref="Task"/> representing the startup operation.
        /// </returns>
        public async Task Start()
        {
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

            _connection = new ClientConnection(_dispatcher, _serverProcess);
            _connection.Open();

            // TODO: Auto-initialise, and await InitializeResult response .
            _readyCompletion.TrySetResult(null);

            await IsReady;
        }

        /// <summary>
        ///     Stop the language server.
        /// </summary>
        /// <returns>
        ///     A <see cref="Task"/> representing the shutdown operation.
        /// </returns>
        public async Task Stop()
        {
            // TODO: Graceful shutdown.
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

            _readyCompletion = new TaskCompletionSource<object>();
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
            InitializeResult result = await SendRequest<InitializeResult>("initialize", cancellationToken);
            ServerCapabilities = result.Capabilities;
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
