﻿using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LSP.Client.Handlers
{
    /// <summary>
    ///     A delegate-based handler for requests.
    /// </summary>
    /// <typeparam name="TRequest">
    ///     The request message type.
    /// </typeparam>
    public class DelegateRequestHandler<TRequest>
        : DelegateHandler, IInvokeRequestHandler
    {
        /// <summary>
        ///     Create a new <see cref="DelegateRequestHandler{TRequest}"/>.
        /// </summary>
        /// <param name="method">
        ///     The name of the method handled by the handler.
        /// </param>
        /// <param name="handler">
        ///     The <see cref="RequestHandler{TRequest}"/> delegate that implements the handler.
        /// </param>
        public DelegateRequestHandler(string method, RequestHandler<TRequest> handler)
            : base(method)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Handler = handler;
        }

        /// <summary>
        ///     The <see cref="RequestHandler{TRequest}"/> delegate that implements the handler.
        /// </summary>
        public RequestHandler<TRequest> Handler { get; }

        /// <summary>
        ///     The kind of handler.
        /// </summary>
        public override HandlerKind Kind => HandlerKind.Request;

        /// <summary>
        ///     Invoke the handler.
        /// </summary>
        /// <param name="request">
        ///     The request message.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task{TResult}"/> representing the operation.
        /// </returns>
        public async Task<object> Invoke(JObject request, CancellationToken cancellationToken)
        {
            await Handler(
                request != null ? request.ToObject<TRequest>() : default(TRequest),
                cancellationToken
            );

            return null;
        }
    }
}
