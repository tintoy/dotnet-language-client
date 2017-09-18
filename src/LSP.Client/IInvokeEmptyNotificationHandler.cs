namespace LspClient
{
    /// <summary>
    ///     Represents a handler for empty notifications.
    /// </summary>
    public interface IInvokeEmptyNotificationHandler
        : IHandler
    {
        /// <summary>
        ///     Invoke the handler.
        /// </summary>
        void Invoke();
    }
}
