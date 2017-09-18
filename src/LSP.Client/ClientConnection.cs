﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LspClient
{
    /// <summary>
    ///     A client-side LSP connection.
    /// </summary>
    public class ClientConnection
    {
        /// <summary>
        ///     Minimum size of the buffer for receiving headers ("Content-Length: X\r\n\r\n").
        /// </summary>
        const short MinimumHeaderLength = 21;

        /// <summary>
        ///     The queue of outgoing requests.
        /// </summary>
        readonly BlockingCollection<object> _outgoing = new BlockingCollection<object>(new ConcurrentQueue<object>());

        /// <summary>
        ///     The queue of incoming responses.
        /// </summary>
        readonly BlockingCollection<ServerMessage> _incoming = new BlockingCollection<ServerMessage>(new ConcurrentQueue<ServerMessage>());

        /// <summary>
        ///     <see cref="TaskCompletionSource{TResult}"/>s representing completion of responses from the language server (keyed by request Id).
        /// </summary>
        readonly ConcurrentDictionary<int, TaskCompletionSource<ServerMessage>> _responseCompletions = new ConcurrentDictionary<int, TaskCompletionSource<ServerMessage>>();

        /// <summary>
        ///     The input stream.
        /// </summary>
        readonly Stream _input;

        /// <summary>
        ///     The output stream.
        /// </summary>
        readonly Stream _output;

        /// <summary>
        ///     The text encoding to use.
        /// </summary>
        readonly Encoding _encoding;

        /// <summary>
        ///     The dispatcher for notifications and requests from the language server.
        /// </summary>
        readonly ClientDispatcher _dispatcher = new ClientDispatcher();

        /// <summary>
        ///     The next available request Id.
        /// </summary>
        int _nextRequestId = 0;

        /// <summary>
        ///     The cancellation source for the read and write loops.
        /// </summary>
        CancellationTokenSource _cancellationSource;

        /// <summary>
        ///     Cancellation for the read and write loops.
        /// </summary>
        CancellationToken _cancellation;

        /// <summary>
        ///     A <see cref="Task"/> representing the stopping of the connection's send, receive, and dispatch loops.
        /// </summary>
        Task _hasStoppedTask = Task.CompletedTask;

        /// <summary>
        ///     A <see cref="Task"/> representing the connection's receive loop.
        /// </summary>
        Task _sendLoop;

        /// <summary>
        ///     A <see cref="Task"/> representing the connection's send loop.
        /// </summary>
        Task _receiveLoop;

        /// <summary>
        ///     A <see cref="Task"/> representing the connection's dispatch loop.
        /// </summary>
        Task _dispatchLoop;

        /// <summary>
        ///     Create a new <see cref="ClientConnection"/>.
        /// </summary>
        /// <param name="serverProcess">
        ///     A <see cref="Process"/> representing the language server.
        /// </param>
        public ClientConnection(Process serverProcess)
            : this(input: serverProcess.StandardOutput.BaseStream, output: serverProcess.StandardInput.BaseStream, encoding: serverProcess.StandardInput.Encoding)
        {
        }

        /// <summary>
        ///     Create a new <see cref="ClientConnection"/>.
        /// </summary>
        /// <param name="input">
        ///     The input stream.
        /// </param>
        /// <param name="output">
        ///     The output stream.
        /// </param>
        /// <param name="encoding">
        ///     The text encoding to use.
        /// </param>
        public ClientConnection(Stream input, Stream output, Encoding encoding)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            if (!input.CanRead)
                throw new ArgumentException("Input stream does not support reading.", nameof(input));

            if (output == null)
                throw new ArgumentNullException(nameof(output));

            if (!output.CanWrite)
                throw new ArgumentException("Output stream does not support reading.", nameof(output));

            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            _input = input;
            _output = output;
            _encoding = encoding;
        }

        /// <summary>
        ///     A task that completes when the connection is stopped.
        /// </summary>
        public Task HasStopped => _hasStoppedTask;

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
        ///     Start the connection.
        /// </summary>
        public void Start()
        {
            _cancellationSource = new CancellationTokenSource();
            _cancellation = _cancellationSource.Token;
            _sendLoop = SendLoop();
            _receiveLoop = ReceiveLoop();
            _dispatchLoop = DispatchLoop();
            _hasStoppedTask = Task.WhenAll(_sendLoop, _receiveLoop, _dispatchLoop);
        }

        /// <summary>
        ///     Stop the connection.
        /// </summary>
        public void Stop()
        {
            _cancellationSource.Cancel();
            _sendLoop = null;
            _receiveLoop = null;
            _dispatchLoop = null;
        }

        /// <summary>
        ///     Send a notification to the language server.
        /// </summary>
        /// <param name="method">
        ///     The notification method name.
        /// </param>
        public void SendNotification(string method)
        {
            _outgoing.TryAdd(new ClientMessage
            {
                // No Id means it's a notification.
                Method = method,
                Params = new JObject()
            });
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
            _outgoing.TryAdd(new ClientMessage
            {
                // No Id means it's a notification.
                Method = method,
                Params = notification != null ? JObject.FromObject(notification) : null
            });
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
        public async Task SendRequest(string method, object request, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (String.IsNullOrWhiteSpace(method))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(method)}.", nameof(method));

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            int requestId = Interlocked.Increment(ref _nextRequestId);

            TaskCompletionSource<ServerMessage> completion = new TaskCompletionSource<ServerMessage>(state: requestId);
            cancellationToken.Register(
                () => completion.TrySetCanceled(cancellationToken)
            );

            _responseCompletions.TryAdd(requestId, completion);

            _outgoing.TryAdd(new ClientMessage
            {
                Id = requestId,
                Method = method,
                Params = request != null ? JObject.FromObject(request) : null
            });

            await completion.Task;
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
        public async Task<TResponse> SendRequest<TResponse>(string method, object request, CancellationToken cancellation = default(CancellationToken))
        {
            if (String.IsNullOrWhiteSpace(method))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(method)}.", nameof(method));

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            int requestId = Interlocked.Increment(ref _nextRequestId);

            TaskCompletionSource<ServerMessage> completion = new TaskCompletionSource<ServerMessage>(state: requestId);
            cancellation.Register(
                () => completion.TrySetCanceled(cancellation)
            );

            _responseCompletions.TryAdd(requestId, completion);

            _outgoing.TryAdd(new ClientMessage
            {
                Id = requestId,
                Method = method,
                Params = request != null ? JObject.FromObject(request) : null
            });

            ServerMessage response = await completion.Task;

            if (response.Result != null)
                return response.Result.ToObject<TResponse>();
            else
                return default(TResponse);
        }

        /// <summary>
        ///     The connection's message-send loop.
        /// </summary>
        /// <returns>
        ///     A <see cref="Task"/> representing the loop's activity.
        /// </returns>
        async Task SendLoop()
        {
            await Task.Yield();

            try
            {
                while (_outgoing.TryTake(out object outgoing, -1, _cancellation))
                {
                    if (outgoing is ClientMessage message)
                    {
                        if (message.Id != null)
                            Log.Information("Sending outgoing {RequestMethod} request {RequestId}...", message.Method, message.Id);
                        else
                            Log.Information("Sending outgoing {RequestMethod} notification...", message.Method);

                        string requestPayload = JsonConvert.SerializeObject(message);

                        byte[] buffer = _encoding.GetBytes(
                            $"Content-Length: {requestPayload.Length}\r\n\r\n{requestPayload}"
                        );
                        await _output.WriteAsync(buffer, 0, buffer.Length, _cancellation);
                        await _output.FlushAsync(_cancellation);

                        if (message.Id != null)
                            Log.Information("Sent outgoing {RequestMethod} request {RequestId}.", message.Method, message.Id);
                        else
                            Log.Information("Sent outgoing {RequestMethod} notification.", message.Method);
                    }
                    else if (outgoing is JsonRpc.Error errorResponse)
                    {
                        Log.Information("Sending outgoing error response {RequestId} ({ErrorMessage})...", errorResponse.Id, errorResponse.Message);

                        string requestPayload = JsonConvert.SerializeObject(errorResponse);

                        byte[] buffer = _encoding.GetBytes(
                            $"Content-Length: {requestPayload.Length}\r\n\r\n{requestPayload}"
                        );
                        await _output.WriteAsync(buffer, 0, buffer.Length, _cancellation);
                        await _output.FlushAsync(_cancellation);

                        Log.Information("Sent outgoing error response {RequestId}.", errorResponse.Id);
                    }
                    else
                        Log.Error("Unexpected outgoing message type '{0}'.", outgoing.GetType().AssemblyQualifiedName);
                }
            }
            catch (OperationCanceledException operationCanceled)
            {
                // Like tears in rain, time to die.
                if (operationCanceled.CancellationToken != _cancellation)
                    throw;
            }
        }

        /// <summary>
        ///     The connection's message-receive loop.
        /// </summary>
        /// <returns>
        ///     A <see cref="Task"/> representing the loop's activity.
        /// </returns>
        async Task ReceiveLoop()
        {
            await Task.Yield();

            try
            {
                while (!_cancellation.IsCancellationRequested)
                {
                    Log.Verbose("Reading response headers...");

                    byte[] buffer = new byte[300];
                    int bytesRead = await _input.ReadAsync(buffer, 0, MinimumHeaderLength, _cancellation);

                    Log.Verbose("Read {ByteCount} bytes from input stream.", bytesRead);

                    if (bytesRead == 0)
                        return; // Stream closed.

                    const byte CR = (byte)'\r';
                    const byte LF = (byte)'\n';

                    while (bytesRead < MinimumHeaderLength ||
                           buffer[bytesRead - 4] != CR || buffer[bytesRead - 3] != LF ||
                           buffer[bytesRead - 2] != CR || buffer[bytesRead - 1] != LF)
                    {
                        Log.Verbose("Reading additional data from input stream...");

                        var additionalBytesRead = await _input.ReadAsync(buffer, bytesRead, 1, _cancellation);
                        if (additionalBytesRead == 0)
                            return; // no more _input, mitigates endless loop here.

                        Log.Verbose("Read {ByteCount} bytes of additional data from input stream.", additionalBytesRead);

                        bytesRead += additionalBytesRead;
                    }

                    string headers = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    Log.Verbose("Got raw headers: {Headers}", headers);

                    if (String.IsNullOrWhiteSpace(headers))
                        return; // Stream closed.

                    Log.Verbose("Read response headers {Headers}.", headers);

                    int contentLength = Int32.Parse(
                        headers.Substring("Content-Length: ".Length).Trim()
                    );

                    Log.Verbose("Reading response body ({ExpectedByteCount} bytes expected).", contentLength);

                    var requestBuffer = new byte[contentLength];
                    var received = 0;
                    while (received < contentLength)
                    {
                        Log.Verbose("Reading segment of incoming request body ({ReceivedByteCount} of {TotalByteCount} bytes so far)...", received, contentLength);

                        var payloadBytesRead = await _input.ReadAsync(requestBuffer, received, requestBuffer.Length - received, _cancellation);
                        if (payloadBytesRead == 0)
                        {
                            Log.Warning("Bailing out of reading payload (no_more_input after {ByteCount} bytes)...", received);

                            return;
                        }

                        Log.Verbose("Read segment of incoming request body ({ReceivedByteCount} of {TotalByteCount} bytes so far).", received, contentLength);

                        received += payloadBytesRead;
                    }

                    Log.Verbose("Received entire payload ({ReceivedByteCount} bytes).", received);

                    string responseBody = Encoding.UTF8.GetString(requestBuffer);
                    ServerMessage message = JsonConvert.DeserializeObject<ServerMessage>(responseBody);
                    Log.Verbose("Read response body {ResponseBody}.", responseBody);

                    // Request / notification from server.
                    if (message.Id != null)
                    {
                        int requestId = Convert.ToInt32(message.Id);
                        TaskCompletionSource<ServerMessage> completion;
                        if (_responseCompletions.TryGetValue(requestId, out completion))
                        {
                            Log.Information("Received {RequestMethod} response {RequestId} from language server: {ResponseParameters}",
                                message.Method,
                                message.Id,
                                message.Params?.ToString(Formatting.None)
                            );

                            Log.Information("Completing request {RequestId}.", requestId);

                            completion.TrySetResult(message);
                        }
                        else
                        {
                            Log.Information("Received {RequestMethod} request {RequestId} from language server: {RequestParameters}",
                                message.Method,
                                message.Id,
                                message.Params?.ToString(Formatting.None)
                            );
                        }

                        // Publish.
                        _incoming.TryAdd(message);
                    }
                    else
                    {
                        Log.Information("Received {NotificationMethod} notification from language server: {NotificationParameters}",
                            message.Method,
                            message.Params.ToString(Formatting.None)
                        );

                        // Publish.
                        _incoming.TryAdd(message);
                    }
                }
            }
            catch (OperationCanceledException operationCanceled)
            {
                // Like tears in rain, time to die.
                if (operationCanceled.CancellationToken != _cancellation)
                    throw;
            }
        }

        /// <summary>
        ///     The connection's message-dispatch loop.
        /// </summary>
        /// <returns>
        ///     A <see cref="Task"/> representing the loop's activity.
        /// </returns>
        async Task DispatchLoop()
        {
            await Task.Yield();

            try
            {
                while (_incoming.TryTake(out ServerMessage message, -1, _cancellation))
                {
                    if (message.Id != null)
                        Log.Information("Dispatching incoming {RequestMethod} request {RequestId}...", message.Method, message.Id);
                    else
                        

                    if (message.Id != null)
                    {
                        // Request.
                        Task<object> handlerTask = _dispatcher.TryHandleRequest(message.Method, message.Params, _cancellation);
                        if (handlerTask == null)
                        {
                            Log.Warning("Unable to dispatch incoming {RequestMethod} request {RequestId} (no handler registered).", message.Method, message.Id);

                            _outgoing.TryAdd(
                                new JsonRpc.Server.Messages.MethodNotFound(message.Id)
                            );
                        }

                        object result;
                        try
                        {
                            result = await handlerTask;
                        }
                        catch (Exception handlerError)
                        {
                            Log.Error(handlerError, "Unable to dispatch incoming {RequestMethod} request {RequestId} (unexpected error raised by handler).", message.Method, message.Id);
                            _outgoing.TryAdd(new JsonRpc.Error(
                                message.Id,
                                new JsonRpc.Server.Messages.ErrorMessage(
                                    code: 500,
                                    message: "Error processing request: " + handlerError.Message,
                                    data: handlerError.ToString()
                                )
                            ));

                            continue;
                        }

                        _outgoing.TryAdd(new ClientMessage
                        {
                            Id = message.Id,
                            Method = message.Method,
                            Result = result != null ? JObject.FromObject(result) : null
                        });
                    }
                    else
                    {
                        // Notification.
                        Log.Information("Dispatching incoming {NotificationMethod} notification...", message.Method);

                        bool handled;

                        try
                        {
                            if (message.Params != null)
                                handled = _dispatcher.TryHandleEmptyNotification(message.Method);
                            else
                                handled = _dispatcher.TryHandleNotification(message.Method, message.Params);
                        }
                        catch (Exception handlerError)
                        {
                            Log.Error(handlerError, "Unable to dispatch incoming {NotificationMethod} notification (unexpected error raised by handler).", message.Method);

                            continue;
                        }

                        if (!handled)
                        {
                            Log.Warning("Unable to dispatch incoming {NotificationMethod} notification (no handler registered).", message.Method);

                            continue;
                        }
                        else
                            Log.Information("Dispatched incoming {NotificationMethod} notification.", message.Method);
                    }
                }
            }
            catch (OperationCanceledException operationCanceled)
            {
                // Like tears in rain, time to die.
                if (operationCanceled.CancellationToken != _cancellation)
                    throw;
            }
        }
    }
}
