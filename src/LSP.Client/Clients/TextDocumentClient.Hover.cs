using Lsp.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
    
namespace LSP.Client.Clients
{
    using Utilities;

    /// <summary>
    ///     Client for the LSP Text Document API.
    /// </summary>
    public partial class TextDocumentClient
    {
        /// <summary>
        ///     Request hover information at the specified document position.
        /// </summary>
        /// <param name="filePath">
        ///     The full file-system path of the text document.
        /// </param>
        /// <param name="line">
        ///     The target line (0-based).
        /// </param>
        /// <param name="column">
        ///     The target column (0-based).
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     A <see cref="Task{TResult}"/> that resolves to the hover information or <c>null</c> if no hover information is available at the specified position.
        /// </returns>
        public Task<Hover> Hover(string filePath, int line, int column, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(filePath)}.", nameof(filePath));

            Uri documentUri = DocumentUri.FromFileSystemPath(filePath);

            return Hover(documentUri, line, column, cancellationToken);
        }

        /// <summary>
        ///     Request hover information at the specified document position.
        /// </summary>
        /// <param name="documentUri">
        ///     The document URI.
        /// </param>
        /// <param name="line">
        ///     The target line (0-based).
        /// </param>
        /// <param name="character">
        ///     The target column (0-based).
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     A <see cref="Task{TResult}"/> that resolves to the hover information or <c>null</c> if no hover information is available at the specified position.
        /// </returns>
        public async Task<Hover> Hover(Uri documentUri, int line, int character, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (documentUri == null)
                throw new ArgumentNullException(nameof(documentUri));

            var request = new TextDocumentPositionParams
            {
                TextDocument = new TextDocumentItem
                {
                    Uri = documentUri
                },
                Position = new Position
                {
                    Line = line,
                    Character = character
                }
            };

            return await Client.SendRequest<Hover>("textDocument/hover", request, cancellationToken).ConfigureAwait(false);
        }
    }
}
