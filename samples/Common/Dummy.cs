using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer;
using OmniSharp.Extensions.LanguageServer.Models;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common
{
    /// <summary>
    ///     Parameters for the "dummy" request and "dummy/notify" notification.
    /// </summary>
    public class DummyParams
    {
        /// <summary>
        ///     A textual message (der).
        /// </summary>
        [JsonProperty("message")]
        public string Message { get; set; }
    }

    /// <summary>
    ///     Represents a handler for the "dummy" request.
    /// </summary>
    [Method("dummy")]
    public interface IDummyRequestHandler
        : IRequestHandler<DummyParams>
    {
    }

    /// <summary>
    ///     A handler for the "dummy" request.
    /// </summary>
    public class DummyHandler
        : IDummyRequestHandler
    {
        /// <summary>
        ///     Create a new <see cref="DummyHandler"/>.
        /// </summary>
        /// <param name="server">
        ///     The language server.
        /// </param>
        public DummyHandler(ILanguageServer server)
        {
            if (server == null)
                throw new ArgumentNullException(nameof(server));

            Server = server;
        }

        /// <summary>
        ///     The language server.
        /// </summary>
        ILanguageServer Server { get; }

        /// <summary>
        ///     Handle the "dummy" request.
        /// </summary>
        /// <param name="request">
        ///     The request parameters.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        public Task Handle(DummyParams request, CancellationToken cancellationToken)
        {
            Log.Information("DummyHandler got request {@Request}", request);

            Server.LogMessage(new LogMessageParams
            {
                Message = "Hello from DummyHandler :-)",
                Type = MessageType.Info
            });

            char[] message = request.Message.ToCharArray();
            Array.Reverse(message);

            Server.SendNotification("dummy/notify", new DummyParams
            {
                Message = new String(message)
            });

            return Task.CompletedTask;
        }
    }
}
