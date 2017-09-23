using System;

namespace LSP.Client.Dispatcher
{
    using Handlers;

    /// <summary>
    ///     Extension methods for <see cref="ClientDispatcher"/> that enable various styles of handler registration.
    /// </summary>
    public static class ClientDispatcherExtensions
    {
        /// <summary>
        ///     Register a delegate with the <see cref="ClientDispatcher"/> to handle the specified empty notification type.
        /// </summary>
        /// <param name="clientDispatcher">
        ///     The <see cref="ClientDispatcher"/>.
        /// </param>
        /// <param name="method">
        ///     The name of the notification method to handle.
        /// </param>
        /// <param name="handler">
        ///     A <see cref="EmptyNotificationHandler"/> delegate that will be called to handle the notification.
        /// </param>
        /// <returns>
        ///     An <see cref="IDisposable"/> representing the registration.
        /// </returns>
        public static IDisposable RegisterEmptyNotificationHandler(this ClientDispatcher clientDispatcher, string method, EmptyNotificationHandler handler)
        {
            if (clientDispatcher == null)
                throw new ArgumentNullException(nameof(clientDispatcher));

            if (String.IsNullOrWhiteSpace(method))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(method)}.", nameof(method));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            return clientDispatcher.RegisterHandler(
                new DelegateEmptyNotificationHandler(method, handler)
            );
        }

        /// <summary>
        ///     Register a delegate with the <see cref="ClientDispatcher"/> to handle the specified notification type.
        /// </summary>
        /// <param name="clientDispatcher">
        ///     The <see cref="ClientDispatcher"/>.
        /// </param>
        /// <param name="method">
        ///     The name of the notification method to handle.
        /// </param>
        /// <param name="handler">
        ///     A <see cref="NotificationHandler{TNotification}"/> delegate that will be called to handle the notification.
        /// </param>
        /// <returns>
        ///     An <see cref="IDisposable"/> representing the registration.
        /// </returns>
        public static IDisposable RegisterNotificationHandler<TNotification>(this ClientDispatcher clientDispatcher, string method, NotificationHandler<TNotification> handler)
        {
            if (clientDispatcher == null)
                throw new ArgumentNullException(nameof(clientDispatcher));

            if (String.IsNullOrWhiteSpace(method))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(method)}.", nameof(method));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            return clientDispatcher.RegisterHandler(
                new DelegateNotificationHandler<TNotification>(method, handler)
            );
        }

        /// <summary>
        ///     Register a delegate with the <see cref="ClientDispatcher"/> to handle the specified request type.
        /// </summary>
        /// <param name="clientDispatcher">
        ///     The <see cref="ClientDispatcher"/>.
        /// </param>
        /// <param name="method">
        ///     The name of the request method to handle.
        /// </param>
        /// <param name="handler">
        ///     A <see cref="RequestHandler{TRequest}"/> delegate that will be called to handle the request.
        /// </param>
        /// <returns>
        ///     An <see cref="IDisposable"/> representing the registration.
        /// </returns>
        public static IDisposable RegisterNotificationHandler<TRequest>(this ClientDispatcher clientDispatcher, string method, RequestHandler<TRequest> handler)
        {
            if (clientDispatcher == null)
                throw new ArgumentNullException(nameof(clientDispatcher));

            if (String.IsNullOrWhiteSpace(method))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(method)}.", nameof(method));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            return clientDispatcher.RegisterHandler(
                new DelegateRequestHandler<TRequest>(method, handler)
            );
        }

        /// <summary>
        ///     Register a delegate with the <see cref="ClientDispatcher"/> to handle the specified request type.
        /// </summary>
        /// <param name="clientDispatcher">
        ///     The <see cref="ClientDispatcher"/>.
        /// </param>
        /// <param name="method">
        ///     The name of the request method to handle.
        /// </param>
        /// <param name="handler">
        ///     A <see cref="RequestHandler{TRequest, TResponse}"/> delegate that will be called to handle the request.
        /// </param>
        /// <returns>
        ///     An <see cref="IDisposable"/> representing the registration.
        /// </returns>
        public static IDisposable RegisterNotificationHandler<TRequest, TResponse>(this ClientDispatcher clientDispatcher, string method, RequestHandler<TRequest, TResponse> handler)
        {
            if (clientDispatcher == null)
                throw new ArgumentNullException(nameof(clientDispatcher));

            if (String.IsNullOrWhiteSpace(method))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(method)}.", nameof(method));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            return clientDispatcher.RegisterHandler(
                new DelegateRequestResponseHandler<TRequest, TResponse>(method, handler)
            );
        }

        /// <summary>
        ///     Register a JSON-RPC handler for an empty notification.
        /// </summary>
        /// <param name="clientDispatcher">
        ///     The <see cref="ClientDispatcher"/>.
        /// </param>
        /// <param name="method">
        ///     The name of the notification method to handle.
        /// </param>
        /// <param name="handler">
        ///     The JSON-RPC <see cref="JsonRpc.INotificationHandler"/>.
        /// </param>
        /// <returns>
        ///     An <see cref="IDisposable"/> representing the registration.
        /// </returns>
        public static IDisposable RegisterEmptyNotificationHandler(this ClientDispatcher clientDispatcher, string method, JsonRpc.INotificationHandler handler)
        {
            if (clientDispatcher == null)
                throw new ArgumentNullException(nameof(clientDispatcher));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            return clientDispatcher.RegisterHandler(
                new JsonRpcEmptyNotificationHandler(method, handler)
            );
        }

        /// <summary>
        ///     Register a JSON-RPC handler for a notification.
        /// </summary>
        /// <typeparam name="TNotification">
        ///     The notification message type.s
        /// </typeparam>
        /// <param name="clientDispatcher">
        ///     The <see cref="ClientDispatcher"/>.
        /// </param>
        /// <param name="method">
        ///     The name of the notification method to handle.
        /// </param>
        /// <param name="handler">
        ///     The JSON-RPC <see cref="JsonRpc.INotificationHandler{TNotification}"/>.
        /// </param>
        /// <returns>
        ///     An <see cref="IDisposable"/> representing the registration.
        /// </returns>
        public static IDisposable RegisterNotificationHandler<TNotification>(this ClientDispatcher clientDispatcher, string method, JsonRpc.INotificationHandler<TNotification> handler)
        {
            if (clientDispatcher == null)
                throw new ArgumentNullException(nameof(clientDispatcher));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            return clientDispatcher.RegisterHandler(
                new JsonRpcNotificationHandler<TNotification>(method, handler)
            );
        }
    }
}
