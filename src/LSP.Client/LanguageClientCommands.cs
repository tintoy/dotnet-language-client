using System;

namespace LSP.Client
{
    using Handlers;
    using Lsp.Models;
    using Newtonsoft.Json.Linq;
    using System.Threading.Tasks;

    /// <summary>
    ///     Extension methods for <see cref="LanguageClient"/> for invoking commands.
    /// </summary>
    public static class LanguageClientCommands
    {
        /// <summary>
        ///     Notify the language server that workspace configuration has changed.
        /// </summary>
        /// <param name="languageClient">
        ///     The language client.
        /// </param>
        /// <param name="configuration">
        ///     A <see cref="JObject"/> representing the workspace configuration (or a subset thereof).
        /// </param>
        public static void DidChangeConfiguration(this LanguageClient languageClient, JObject configuration)
        {
            if (languageClient == null)
                throw new ArgumentNullException(nameof(languageClient));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            languageClient.SendNotification("workspace/didChangeConfiguration", configuration);
        }
    }
}
