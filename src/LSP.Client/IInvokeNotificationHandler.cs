using Newtonsoft.Json.Linq;

namespace LspClient
{
    /// <summary>
    ///     Represents a handler for notifications.
    /// </summary>
    public interface IInvokeNotificationHandler
        : IHandler
    {
        /// <summary>
        ///     Invoke the handler.
        /// </summary>
        /// <param name="notification">
        ///     The notification message.
        /// </param>
        void Invoke(JObject notification);
    }
}
