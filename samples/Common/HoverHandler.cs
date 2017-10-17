using OmniSharp.Extensions.LanguageServer.Protocol;
using System;
using System.Collections.Generic;
using System.Text;
using OmniSharp.Extensions.LanguageServer.Capabilities.Client;
using OmniSharp.Extensions.LanguageServer.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Common
{
    /// <summary>
    ///     Handler for LSP "textDocument/hover" requests.
    /// </summary>
    public class HoverHandler
        : IHoverHandler
    {
        /// <summary>
        ///     Create a new <see cref="HoverHandler"/>.
        /// </summary>
        public HoverHandler()
        {
        }

        /// <summary>
        ///     Registration options for the hover handler.
        /// </summary>
        public TextDocumentRegistrationOptions TextDocumentRegistrationOptions { get; } = new TextDocumentRegistrationOptions
        {
            DocumentSelector = new DocumentSelector(
                new DocumentFilter
                {
                    Language = "xml",
                    Pattern = "**/*.csproj"
                }
            )
        };

        /// <summary>
        ///     The client's hover capabilities.
        /// </summary>
        public HoverCapability Capabilities { get; private set; }

        /// <summary>
        ///     Handle a hover request.
        /// </summary>
        /// <param name="request">
        ///     The hover request.
        /// </param>
        /// <param name="token">
        ///     A cancellation token that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     The <see cref="Hover"/>, or <c>null</c> if no hover information is available at the target document position.
        /// </returns>
        public Task<Hover> Handle(TextDocumentPositionParams request, CancellationToken token)
        {
            return Task.FromResult(new Hover
            {
                Range = new Range(
                    start: request.Position,
                    end: request.Position
                ),
                Contents = new MarkedStringContainer(
                    $"Hover for {request.Position.Line + 1},{request.Position.Character + 1} in '{request.TextDocument.Uri}'."
                )
            });
        }

        /// <summary>
        ///     Get registration options for the hover handler.
        /// </summary>
        /// <returns>
        ///     The <see cref="TextDocumentRegistrationOptions"/>.
        /// </returns>
        public TextDocumentRegistrationOptions GetRegistrationOptions() => TextDocumentRegistrationOptions;

        /// <summary>
        ///     Called to provide information about the client's hover capabilities.
        /// </summary>
        /// <param name="capabilities">
        ///     The client's hover capabilities.
        /// </param>
        public void SetCapability(HoverCapability capabilities)
        {
            Capabilities = capabilities;
        }
    }
}
