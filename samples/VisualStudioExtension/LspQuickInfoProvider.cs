using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace VisualStudioExtension
{
    /// <summary>
    ///     QuickInfo provider for LSP-based Hover information.
    /// </summary>
    [Export(typeof(IQuickInfoSourceProvider))]
    [Name("LSP Quick Info Controller")]
    [ContentType("XML")]
    internal class LspQuickInfoProvider
        : IQuickInfoSourceProvider
    {
        /// <summary>
        ///     Create a new <see cref="LspQuickInfoProvider"/>.
        /// </summary>
        [ImportingConstructor]
        public LspQuickInfoProvider()
        {
        }

        /// <summary>
        ///     Create a QuickInfo source for the specified text buffer.
        /// </summary>
        /// <param name="textBuffer">
        ///     The <see cref="ITextBuffer"/>.
        /// </param>
        /// <returns>
        ///     The <see cref="IQuickInfoSource"/>, or <c>null</c> if no QuickInfo will be offered.
        /// </returns>
        public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            if (textBuffer == null)
                throw new ArgumentNullException(nameof(textBuffer));

            return new LspQuickInfoSource();
        }
    }
}
