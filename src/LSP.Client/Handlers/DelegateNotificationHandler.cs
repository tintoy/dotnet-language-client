using Newtonsoft.Json.Linq;
using System;

namespace LSP.Client.Handlers
{
    /// <summary>
    ///     A delegate-based handler for notifications.
    /// </summary>
    /// <typeparam name="TNotification">
    ///     The notification message type.
    /// </typeparam>
    public class DelegateNotificationHandler<TNotification>
        : DelegateHandler, IInvokeNotificationHandler
    {
        /// <summary>
        ///     Create a new <see cref="DelegateNotificationHandler{TNotification}"/>.
        /// </summary>
        /// <param name="method">
        ///     The name of the method handled by the handler.
        /// </param>
        /// <param name="handler">
        ///     The <see cref="NotificationHandler{TNotification}"/> delegate that implements the handler.
        /// </param>
        public DelegateNotificationHandler(string method, NotificationHandler<TNotification> handler)
            : base(method)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Handler = handler;
        }

        /// <summary>
        ///     The <see cref="NotificationHandler{TNotification}"/> delegate that implements the handler.
        /// </summary>
        public NotificationHandler<TNotification> Handler { get; }

        /// <summary>
        ///     The kind of handler.
        /// </summary>
        public override HandlerKind Kind => HandlerKind.EmptyNotification;

        /// <summary>
        ///     Invoke the handler.
        /// </summary>
        /// <param name="notification">
        ///     The notification message.
        /// </param>
        public void Invoke(JObject notification) => Handler(
            notification != null ? notification.ToObject<TNotification>() : default(TNotification)
        );
    }
}
