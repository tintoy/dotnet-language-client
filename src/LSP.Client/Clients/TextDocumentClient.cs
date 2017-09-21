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
    public class TextDocumentClient
    {
        /// <summary>
        ///     Create a new <see cref="TextDocumentClient"/>.
        /// </summary>
        /// <param name="client">
        ///     The language client providing the API.
        /// </param>
        public TextDocumentClient(LanguageClient client)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            Client = client;
        }

        /// <summary>
        ///     The language client providing the API.
        /// </summary>
        public LanguageClient Client { get; }

        /// <summary>
        ///     Notify the language server that the client has opened a text document.
        /// </summary>
        /// <param name="filePath">
        ///     The full path to the text document.
        /// </param>
        /// <param name="languageId">
        ///     The document language type (e.g. "xml").
        /// </param>
        /// <param name="version">
        ///     The document version (optional).
        /// </param>
        /// <remarks>
        ///     Will automatically populate the document text, if available.
        /// </remarks>
        public void DidOpen(string filePath, string languageId, int version = 1)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(filePath)}.", nameof(filePath));

            string text = null;
            if (File.Exists(filePath))
                text = File.ReadAllText(filePath);

            DidOpen(
                DocumentUri.FromFileSystemPath(filePath),
                languageId,
                text,
                version
            );
        }

        /// <summary>
        ///     Notify the language server that the client has opened a text document.
        /// </summary>
        /// <param name="filePath">
        ///     The full file-system path of the text document.
        /// </param>
        /// <param name="languageId">
        ///     The document language type (e.g. "xml").
        /// </param>
        /// <param name="text">
        ///     The document text (pass null to have the language server retrieve the text itself).
        /// </param>
        /// <param name="version">
        ///     The document version (optional).
        /// </param>
        public void DidOpen(string filePath, string languageId, string text, int version = 1)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(filePath)}.", nameof(filePath));

            Uri documentUri = DocumentUri.FromFileSystemPath(filePath);

            DidOpen(documentUri, languageId, text, version);
        }

        /// <summary>
        ///     Notify the language server that the client has opened a text document.
        /// </summary>
        /// <param name="documentUri">
        ///     The document URI.
        /// </param>
        /// <param name="languageId">
        ///     The document language type (e.g. "xml").
        /// </param>
        /// <param name="text">
        ///     The document text.
        /// </param>
        /// <param name="version">
        ///     The document version (optional).
        /// </param>
        public void DidOpen(Uri documentUri, string languageId, string text, int version = 1)
        {
            if (documentUri == null)
                throw new ArgumentNullException(nameof(documentUri));

            Client.SendNotification("textDocument/didOpen", new DidOpenTextDocumentParams
            {
                TextDocument = new TextDocumentItem
                {
                    Text = text,
                    LanguageId = languageId,
                    Version = version,
                    Uri = documentUri
                }
            });
        }

        /// <summary>
        ///     Notify the language server that the client has closeed a text document.
        /// </summary>
        /// <param name="filePath">
        ///     The full file-system path of the text document.
        /// </param>
        public void DidClose(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(filePath)}.", nameof(filePath));

            DidClose(
                DocumentUri.FromFileSystemPath(filePath)
            );
        }

        /// <summary>
        ///     Notify the language server that the client has closeed a text document.
        /// </summary>
        /// <param name="documentUri">
        ///     The document URI.
        /// </param>
        public void DidClose(Uri documentUri)
        {
            if (documentUri == null)
                throw new ArgumentNullException(nameof(documentUri));

            Client.SendNotification("textDocument/didClose", new DidCloseTextDocumentParams
            {
                TextDocument = new TextDocumentItem
                {
                    Uri = documentUri
                }
            });
        }

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
