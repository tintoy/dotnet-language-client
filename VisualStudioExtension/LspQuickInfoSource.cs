using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;

namespace VisualStudioExtension
{
    /// <summary>
    ///     A QuickInfo source that uses Hover content from an LSP language server.
    /// </summary>
    public class LspQuickInfoSource
        : IQuickInfoSource
    {
        /// <summary>
        ///     Create a new <see cref="LspQuickInfoSource"/>.
        /// </summary>
        public LspQuickInfoSource()
        {
        }

        /// <summary>
        ///     Dispose of resources being used by the <see cref="LspQuickInfoSource"/>.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        ///     Augment a QuickInfo session.
        /// </summary>
        /// <param name="session">
        ///     The target QuickInfo session.
        /// </param>
        /// <param name="quickInfoContent">
        ///     The current QuickInfo content.
        /// </param>
        /// <param name="applicableToSpan">
        ///     An <see cref="ITrackingSpan"/> representing the span of text that the QuickInfo (if any) applies to.
        /// </param>
        public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            applicableToSpan = null;

            SnapshotPoint? triggerPoint = session.GetTriggerPoint(session.TextView.TextSnapshot);
            if (!triggerPoint.HasValue)
                return;

            quickInfoContent.Clear();

            // TODO: Call LanguageClient.TextDocument.Hover.
        }
    }
}
