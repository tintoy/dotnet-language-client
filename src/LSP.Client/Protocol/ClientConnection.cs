using JsonRpc.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LSP.Client.Protocol
{
    using Dispatcher;
    using Handlers;

    /// <summary>
    ///     A client-side LSP connection.
    /// </summary>
    public sealed class ClientConnection
        : IDisposable
    {
        /// <summary>
        ///     Minimum size of the buffer for receiving headers ("Content-Length: X\r\n\r\n").
        /// </summary>
        const short MinimumHeaderLength = 21;

        /// <summary>
        ///     The length of time to wait for the outgoing message queue to drain.
        /// </summary>
        public static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        ///     The encoding used for message headers.
        /// </summary>
        public static Encoding HeaderEncoding = Encoding.ASCII;

        /// <summary>
        ///     The encoding used for message payloads.
        /// </summary>
        public static Encoding PayloadEncoding = Encoding.UTF8;

        /// <summary>
        ///     The queue of outgoing requests.
        /// </summary>
        readonly BlockingCollection<object> _outgoing = new BlockingCollection<object>(new ConcurrentQueue<object>());

        /// <summary>
        ///     The queue of incoming responses.
        /// </summary>
        readonly BlockingCollection<ServerMessage> _incoming = new BlockingCollection<ServerMessage>(new ConcurrentQueue<ServerMessage>());

        /// <summary>
        ///     <see cref="CancellationTokenSource"/>s representing cancellation of requests from the language server (keyed by request Id).
        /// </summary>
        readonly ConcurrentDictionary<string, CancellationTokenSource> _requestCancellations = new ConcurrentDictionary<string, CancellationTokenSource>();

        /// <summary>
        ///     <see cref="TaskCompletionSource{TResult}"/>s representing completion of responses from the language server (keyed by request Id).
        /// </summary>
        readonly ConcurrentDictionary<string, TaskCompletionSource<ServerMessage>> _responseCompletions = new ConcurrentDictionary<string, TaskCompletionSource<ServerMessage>>();

        /// <summary>
        ///     The input stream.
        /// </summary>
        readonly Stream _input;

        /// <summary>
        ///     The output stream.
        /// </summary>
        readonly Stream _output;

        /// <summary>
        ///     The dispatcher for notifications and requests from the language server.
        /// </summary>
        readonly ClientDispatcher _dispatcher;

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
        Task _hasClosedTask = Task.CompletedTask;

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
        /// <param name="dispatcher">
        ///     The <see cref="ClientDispatcher"/> used to dispatch messages to handlers.
        /// </param>
        /// <param name="serverProcess">
        ///     A <see cref="Process"/> representing the language server.
        /// </param>
        public ClientConnection(ClientDispatcher dispatcher, Process serverProcess)
            : this(dispatcher, input: serverProcess.StandardOutput.BaseStream, output: serverProcess.StandardInput.BaseStream)
        {
        }

        /// <summary>
        ///     Create a new <see cref="ClientConnection"/>.
        /// </summary>
        /// <param name="dispatcher">
        ///     The <see cref="ClientDispatcher"/> used to dispatch messages to handlers.
        /// </param>
        /// <param name="input">
        ///     The input stream.
        /// </param>
        /// <param name="output">
        ///     The output stream.
        /// </param>
        public ClientConnection(ClientDispatcher dispatcher, Stream input, Stream output)
        {
            if (dispatcher == null)
                throw new ArgumentNullException(nameof(dispatcher));

            if (input == null)
                throw new ArgumentNullException(nameof(input));

            if (!input.CanRead)
                throw new ArgumentException("Input stream does not support reading.", nameof(input));

            if (output == null)
                throw new ArgumentNullException(nameof(output));

            if (!output.CanWrite)
                throw new ArgumentException("Output stream does not support reading.", nameof(output));

            _dispatcher = dispatcher;
            _input = input;
            _output = output;
        }

        /// <summary>
        ///     Dispose of resources being used by the connection.
        /// </summary>
        public void Dispose()
        {
            Close();
            
            _cancellationSource?.Dispose();
        }

        /// <summary>
        ///     The connection's logger.
        /// </summary>
        ILogger Log { get; } = Serilog.Log.ForContext<ClientConnection>();

        /// <summary>
        ///     Is the connection open?
        /// </summary>
        public bool IsOpen => _sendLoop != null || _receiveLoop != null || _dispatchLoop != null;

        /// <summary>
        ///     A task that completes when the connection is closed.
        /// </summary>
        public Task HasClosed => _hasClosedTask;

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
        ///     Open the connection.
        /// </summary>
        public void Open()
        {
            _cancellationSource = new CancellationTokenSource();
            _cancellation = _cancellationSource.Token;
            _sendLoop = SendLoop();
            _receiveLoop = ReceiveLoop();
            _dispatchLoop = DispatchLoop();
            _hasClosedTask = Task.WhenAll(_sendLoop, _receiveLoop, _dispatchLoop);
        }

        /// <summary>
        ///     Close the connection.
        /// </summary>
        /// <param name="flushOutgoing">
        ///     If <c>true</c>, stop receiving and block until all outgoing messages have been sent.
        /// </param>
        public void Close(bool flushOutgoing = false)
        {
            if (flushOutgoing)
            {
                // Stop receiving.
                _incoming.CompleteAdding();

                // Wait for the outgoing message queue to drain.
                int remainingMessageCount = 0;
                DateTime then = DateTime.Now;
                while (DateTime.Now - then < FlushTimeout)
                {
                    remainingMessageCount = _outgoing.Count;
                    if (remainingMessageCount == 0)
                        break;

                    Thread.Sleep(
                        TimeSpan.FromMilliseconds(200)
                    );
                }

                if (remainingMessageCount > 0)
                    Log.Warning("Failed to flush outgoing messages ({RemainingMessageCount} messages remaining).", _outgoing.Count);
            }

            // Cancel all outstanding requests.
            // This should not be necessary because request cancellation tokens should be linked to _cancellationSource, but better to be sure we won't leave a caller hanging.
            foreach (TaskCompletionSource<ServerMessage> responseCompletion in _responseCompletions.Values)
            {
                responseCompletion.TrySetException(
                    new OperationCanceledException("The request was canceled because the underlying connection was closed.")
                );
            }

            _cancellationSource?.Cancel();
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
            if (string.IsNullOrWhiteSpace(method))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(method)}.", nameof(method));

            if (!IsOpen)
                throw new InvalidOperationException("Not connected to the language server.");

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
            if (string.IsNullOrWhiteSpace(method))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(method)}.", nameof(method));

            if (notification == null)
                throw new ArgumentNullException(nameof(notification));

            if (!IsOpen)
                throw new InvalidOperationException("Not connected to the language server.");

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

            if (!IsOpen)
                throw new InvalidOperationException("Not connected to the language server.");

            string requestId = Interlocked.Increment(ref _nextRequestId).ToString();

            TaskCompletionSource<ServerMessage> responseCompletion = new TaskCompletionSource<ServerMessage>(state: requestId);
            cancellationToken.Register(() =>
            {
                responseCompletion.TrySetException(
                    new OperationCanceledException("The request was canceled via the supplied cancellation token.", cancellationToken)
                );

                // Send notification telling server to cancel the request, if possible.
                if (!_outgoing.IsAddingCompleted)
                {
                    _outgoing.TryAdd(new ClientMessage
                    {
                        Method = "$/cancelRequest",
                        Params = new JObject(
                            new JProperty("id", requestId)
                        )
                    });
                }
            });

            _responseCompletions.TryAdd(requestId, responseCompletion);

            _outgoing.TryAdd(new ClientMessage
            {
                Id = requestId,
                Method = method,
                Params = request != null ? JObject.FromObject(request) : null
            });

            await responseCompletion.Task;
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
        /// <param name="cancellationToken">
        ///     An optional cancellation token that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     A <see cref="Task{TResult}"/> representing the response.
        /// </returns>
        public async Task<TResponse> SendRequest<TResponse>(string method, object request, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (String.IsNullOrWhiteSpace(method))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(method)}.", nameof(method));

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (!IsOpen)
                throw new InvalidOperationException("Not connected to the language server.");

            string requestId = Interlocked.Increment(ref _nextRequestId).ToString();

            TaskCompletionSource<ServerMessage> responseCompletion = new TaskCompletionSource<ServerMessage>(state: requestId);
            cancellationToken.Register(() =>
            {
                responseCompletion.TrySetException(
                    new OperationCanceledException("The request was canceled via the supplied cancellation token.", cancellationToken)
                );

                // Send notification telling server to cancel the request, if possible.
                if (!_outgoing.IsAddingCompleted)
                {
                    _outgoing.TryAdd(new ClientMessage
                    {
                        Method = "$/cancelRequest",
                        Params = new JObject(
                            new JProperty("id", requestId)
                        )
                    });
                }
            });

            _responseCompletions.TryAdd(requestId, responseCompletion);

            _outgoing.TryAdd(new ClientMessage
            {
                Id = requestId,
                Method = method,
                Params = request != null ? JObject.FromObject(request) : null
            });

            ServerMessage response = await responseCompletion.Task;

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
                            Log.Verbose("Sending outgoing {RequestMethod} request {RequestId}...", message.Method, message.Id);
                        else
                            Log.Verbose("Sending outgoing {RequestMethod} notification...", message.Method);

                        await SendMessage(message);

                        if (message.Id != null)
                            Log.Verbose("Sent outgoing {RequestMethod} request {RequestId}.", message.Method, message.Id);
                        else
                            Log.Verbose("Sent outgoing {RequestMethod} notification.", message.Method);
                    }
                    else if (outgoing is JsonRpc.Error errorResponse)
                    {
                        Log.Verbose("Sending outgoing error response {RequestId} ({ErrorMessage})...", errorResponse.Id, errorResponse.Message);

                        await SendMessage(errorResponse);

                        Log.Verbose("Sent outgoing error response {RequestId}.", errorResponse.Id);
                    }
                    else
                        Log.Error("Unexpected outgoing message type '{0}'.", outgoing.GetType().AssemblyQualifiedName);
                }
            }
            catch (OperationCanceledException operationCanceled)
            {
                // Like tears in rain
                if (operationCanceled.CancellationToken != _cancellation)
                    throw; // time to die
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
                while (!_cancellation.IsCancellationRequested && !_incoming.IsAddingCompleted)
                {
                    ServerMessage message = await ReceiveMessage();
                    if (message == null)
                        continue;

                    if (message.Id != null)
                    {
                        // Request or response.
                        if (message.Params != null)
                        {
                            // Request.
                            Log.Verbose("Received {RequestMethod} request {RequestId} from language server: {RequestParameters}",
                                message.Method,
                                message.Id,
                                message.Params?.ToString(Formatting.None)
                            );

                            // Publish.
                            if (!_incoming.IsAddingCompleted)
                                _incoming.TryAdd(message);
                        }
                        else
                        {
                            // Response.
                            string requestId = message.Id.ToString();
                            TaskCompletionSource<ServerMessage> completion;
                            if (_responseCompletions.TryGetValue(requestId, out completion))
                            {
                                if (message.ErrorMessage != null)
                                {
                                    Log.Verbose("Received error response {RequestId} from language server: {@ErrorMessage}",
                                        requestId,
                                        message.ErrorMessage
                                    );

                                    Log.Verbose("Faulting request {RequestId}.", requestId);

                                    completion.TrySetException(new JsonRpcException(
                                        $"Error {message.ErrorMessage.Code}: {message.ErrorMessage.Message}"
                                    ));
                                }
                                else
                                {
                                    Log.Verbose("Received response {RequestId} from language server: {ResponseResult}",
                                        requestId,
                                        message.Result?.ToString(Formatting.None)
                                    );

                                    Log.Verbose("Completing request {RequestId}.", requestId);

                                    completion.TrySetResult(message);
                                }
                            }
                            else
                            {
                                Log.Verbose("Received unexpected response {RequestId} from language server: {ResponseResult}",
                                    requestId,
                                    message.Result?.ToString(Formatting.None)
                                );
                            }
                        }
                    }
                    else
                    {
                        // Notification.
                        Log.Verbose("Received {NotificationMethod} notification from language server: {NotificationParameters}",
                            message.Method,
                            message.Params.ToString(Formatting.None)
                        );

                        // Publish.
                        if (!_incoming.IsAddingCompleted)
                            _incoming.TryAdd(message);
                    }
                }
            }
            catch (OperationCanceledException operationCanceled)
            {
                // Like tears in rain
                if (operationCanceled.CancellationToken != _cancellation)
                    throw; // time to die
            }
        }

        /// <summary>
        ///     Send a message to the language server.
        /// </summary>
        /// <typeparam name="TMessage">
        ///     The type of message to send.
        /// </typeparam>
        /// <param name="message">
        ///     The message to send.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        async Task SendMessage<TMessage>(TMessage message)
            where TMessage : class
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            string payload = JsonConvert.SerializeObject(message);
            byte[] payloadBuffer = PayloadEncoding.GetBytes(payload);

            byte[] headerBuffer = HeaderEncoding.GetBytes(
                $"Content-Length: {payloadBuffer.Length}\r\n\r\n"
            );
            await _output.WriteAsync(headerBuffer, 0, headerBuffer.Length, _cancellation);
            await _output.WriteAsync(payloadBuffer, 0, payloadBuffer.Length, _cancellation);
            await _output.FlushAsync(_cancellation);
        }

        /// <summary>
        ///     Receive a message from the language server.
        /// </summary>
        /// <returns>
        ///     A <see cref="ServerMessage"/> representing the message,
        /// </returns>
        async Task<ServerMessage> ReceiveMessage()
        {
            Log.Verbose("Reading response headers...");

            byte[] buffer = new byte[300];
            int bytesRead = await _input.ReadAsync(buffer, 0, MinimumHeaderLength, _cancellation);

            Log.Verbose("Read {ByteCount} bytes from input stream.", bytesRead);

            if (bytesRead == 0)
                return null; // Stream closed.

            const byte CR = (byte)'\r';
            const byte LF = (byte)'\n';

            while (bytesRead < MinimumHeaderLength ||
                   buffer[bytesRead - 4] != CR || buffer[bytesRead - 3] != LF ||
                   buffer[bytesRead - 2] != CR || buffer[bytesRead - 1] != LF)
            {
                Log.Verbose("Reading additional data from input stream...");

                var additionalBytesRead = await _input.ReadAsync(buffer, bytesRead, 1, _cancellation);
                if (additionalBytesRead == 0)
                    return null; // no more _input, mitigates endless loop here.

                Log.Verbose("Read {ByteCount} bytes of additional data from input stream.", additionalBytesRead);

                bytesRead += additionalBytesRead;
            }

            string headers = HeaderEncoding.GetString(buffer, 0, bytesRead);
            Log.Verbose("Got raw headers: {Headers}", headers);

            if (String.IsNullOrWhiteSpace(headers))
                return null; // Stream closed.

            Log.Verbose("Read response headers {Headers}.", headers);

            Dictionary<string, string> parsedHeaders = ParseHeaders(headers);

            string contentLengthHeader;
            if (!parsedHeaders.TryGetValue("Content-Length", out contentLengthHeader))
            {
                Log.Verbose("Invalid request headers (missing 'Content-Length' header).");

                return null;
            }

            int contentLength = Int32.Parse(contentLengthHeader);

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

                    return null;
                }

                Log.Verbose("Read segment of incoming request body ({ReceivedByteCount} of {TotalByteCount} bytes so far).", received, contentLength);

                received += payloadBytesRead;
            }

            Log.Verbose("Received entire payload ({ReceivedByteCount} bytes).", received);

            string responseBody = PayloadEncoding.GetString(requestBuffer);
            ServerMessage message = JsonConvert.DeserializeObject<ServerMessage>(responseBody);

            Log.Verbose("Read response body {ResponseBody}.", responseBody);

            return message;
        }

        /// <summary>
        ///     Parse request headers.
        /// </summary>
        /// <param name="rawHeaders">
        /// </param>
        /// <returns>
        ///     A <see cref="Dictionary{TKey, TValue}"/> containing the header names and values.
        /// </returns>
        private Dictionary<string, string> ParseHeaders(string rawHeaders)
        {
            if (rawHeaders == null)
                throw new ArgumentNullException(nameof(rawHeaders));

            Dictionary<string, string> headers = new Dictionary<string, string>();
            string[] rawHeaderEntries = rawHeaders.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawHeaderEntry in rawHeaderEntries)
            {
                string[] nameAndValue = rawHeaderEntry.Split(new char[] { ':' }, count: 2);
                if (nameAndValue.Length != 2)
                    continue;

                headers[nameAndValue[0].Trim()] = nameAndValue[1].Trim();
            }

            return headers;
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
                    {
                        // Request.
                        if (message.Method == "$/cancelRequest")
                            CancelRequest(message);
                        else
                            DispatchRequest(message);
                    }
                    else
                    {
                        // Notification.
                        DispatchNotification(message);
                    }
                }
            }
            catch (OperationCanceledException operationCanceled)
            {
                // Like tears in rain
                if (operationCanceled.CancellationToken != _cancellation)
                    throw; // time to die
            }
        }

        /// <summary>
        ///     Dispatch a request.
        /// </summary>
        /// <param name="requestMessage">
        ///     The request message.
        /// </param>
        private void DispatchRequest(ServerMessage requestMessage)
        {
            if (requestMessage == null)
                throw new ArgumentNullException(nameof(requestMessage));

            string requestId = requestMessage.Id.ToString();
            Log.Verbose("Dispatching incoming {RequestMethod} request {RequestId}...", requestMessage.Method, requestId);

            CancellationTokenSource requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellation);
            _requestCancellations.TryAdd(requestId, requestCancellation);

            Task<object> handlerTask = _dispatcher.TryHandleRequest(requestMessage.Method, requestMessage.Params, requestCancellation.Token);
            if (handlerTask == null)
            {
                Log.Warning("Unable to dispatch incoming {RequestMethod} request {RequestId} (no handler registered).", requestMessage.Method, requestId);

                _outgoing.TryAdd(
                    new JsonRpc.Server.Messages.MethodNotFound(requestMessage.Id)
                );
            }

#pragma warning disable CS4014 // Continuation does the work we need; no need to await it as this would tie up the dispatch loop.
            handlerTask.ContinueWith(_ =>
            {
                if (handlerTask.IsCanceled)
                    Log.Verbose("{RequestMethod} request {RequestId} canceled.", requestMessage.Method, requestId);
                else if (handlerTask.IsFaulted)
                {
                    Exception handlerError = handlerTask.Exception.Flatten().InnerExceptions[0];

                    Log.Error(handlerError, "{RequestMethod} request {RequestId} failed (unexpected error raised by handler).", requestMessage.Method, requestId);

                    _outgoing.TryAdd(new JsonRpc.Error(requestId,
                        new JsonRpc.Server.Messages.ErrorMessage(
                            code: 500,
                            message: "Error processing request: " + handlerError.Message,
                            data: handlerError.ToString()
                        )
                    ));
                }
                else if (handlerTask.IsCompleted)
                {
                    Log.Verbose("{RequestMethod} request {RequestId} complete (Result = {@Result}).", requestMessage.Method, requestId, handlerTask.Result);

                    _outgoing.TryAdd(new ClientMessage
                    {
                        Id = requestMessage.Id,
                        Method = requestMessage.Method,
                        Result = handlerTask.Result != null ? JObject.FromObject(handlerTask.Result) : null
                    });
                }

                _requestCancellations.TryRemove(requestId, out CancellationTokenSource cancellation);
                cancellation.Dispose();
            });
#pragma warning restore CS4014 // Continuation does the work we need; no need to await it as this would tie up the dispatch loop.

            Log.Verbose("Dispatched incoming {RequestMethod} request {RequestId}.", requestMessage.Method, requestMessage.Id);
        }

        /// <summary>
        ///     Cancel a request.
        /// </summary>
        /// <param name="requestMessage">
        ///     The request message.
        /// </param>
        void CancelRequest(ServerMessage requestMessage)
        {
            if (requestMessage == null)
                throw new ArgumentNullException(nameof(requestMessage));

            string cancelRequestId = requestMessage.Params?.Value<object>("id")?.ToString();
            if (cancelRequestId != null)
            {
                if (_requestCancellations.TryRemove(cancelRequestId, out CancellationTokenSource requestCancellation))
                {
                    Log.Verbose("Cancel request {RequestId}", requestMessage.Id);
                    requestCancellation.Cancel();
                    requestCancellation.Dispose();
                }
                else
                    Log.Verbose("Received cancellation message for non-existent (or already-completed) request ");
            }
            else
            {
                Log.Warning("Received invalid request cancellation message {MessageId} (missing 'id' parameter).", requestMessage.Id);

                _outgoing.TryAdd(
                    new JsonRpc.Server.Messages.InvalidParams(requestMessage.Id)
                );
            }
        }

        /// <summary>
        ///     Dispatch a notification.
        /// </summary>
        /// <param name="notificationMessage">
        ///     The notification message.
        /// </param>
        void DispatchNotification(ServerMessage notificationMessage)
        {
            if (notificationMessage == null)
                throw new ArgumentNullException(nameof(notificationMessage));

            Log.Verbose("Dispatching incoming {NotificationMethod} notification...", notificationMessage.Method);

            bool handled;

            try
            {
                if (notificationMessage.Params != null)
                    handled = _dispatcher.TryHandleNotification(notificationMessage.Method, notificationMessage.Params);
                else
                    handled = _dispatcher.TryHandleEmptyNotification(notificationMessage.Method);
            }
            catch (Exception handlerError)
            {
                Log.Error(handlerError, "Unable to dispatch incoming {NotificationMethod} notification (unexpected error raised by handler).", notificationMessage.Method);

                return;
            }

            if (handled)
                Log.Verbose("Dispatched incoming {NotificationMethod} notification.", notificationMessage.Method);
            else
                Log.Verbose("Ignored incoming {NotificationMethod} notification (no handler registered).", notificationMessage.Method);
        }
    }
}
