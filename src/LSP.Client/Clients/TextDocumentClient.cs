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
    }
}
