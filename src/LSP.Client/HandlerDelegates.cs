using System.Threading;
using System.Threading.Tasks;

namespace LSP.Client
{
    /// <summary>
    ///     A handler for empty notifications.
    /// </summary>
    public delegate void EmptyNotificationHandler();

    /// <summary>
    ///     A handler for notifications.
    /// </summary>
    /// <typeparam name="TNotification">
    ///     The notification message type.
    /// </typeparam>
    /// <param name="notification">
    ///     The notification message.
    /// </param>
    public delegate void NotificationHandler<TNotification>(TNotification notification);

    /// <summary>
    ///     A handler for requests.
    /// </summary>
    /// <typeparam name="TRequest">
    ///     The request message type.
    /// </typeparam>
    /// <param name="request">
    ///     The request message.
    /// </param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
    /// </param>
    /// <returns>
    ///     A <see cref="Task"/> representing the operation.
    /// </returns>
    public delegate Task RequestHandler<TRequest>(TRequest request, CancellationToken cancellationToken);

    /// <summary>
    ///     A handler for requests that return responses.
    /// </summary>
    /// <typeparam name="TRequest">
    ///     The request message type.
    /// </typeparam>
    /// <typeparam name="TResponse">
    ///     The response message type.
    /// </typeparam>
    /// <param name="request">
    ///     The request message.
    /// </param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
    /// </param>
    /// <returns>
    ///     A <see cref="Task{TResult}"/> representing the operation that resolves to the response message.
    /// </returns>
    public delegate Task<TResponse> RequestHandler<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken);

    /// <summary>
    ///     A handler for log messages sent from the language server to the client.
    /// </summary>
    /// <param name="message">
    ///     The log message.
    /// </param>
    /// <param name="messageType">
    ///     The log message type.
    /// </param>
    public delegate void LogMessageHandler(string message, Lsp.Models.MessageType messageType);
}
