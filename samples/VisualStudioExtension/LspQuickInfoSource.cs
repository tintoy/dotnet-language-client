using OmniSharp.Extensions.LanguageServer.Models;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using Span = Microsoft.VisualStudio.Text.Span;

namespace VisualStudioExtension
{
    using Markdown = Markdown.Xaml.Markdown;

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
        ///     The markdown renderer.
        /// </summary>
        Markdown MarkdownRenderer = new Markdown
        {
            CodeStyle = new Style
            {
                TargetType = typeof(Run),
                Setters =
                {
                    new Setter
                    {
                        Property = TextElement.BackgroundProperty,
                        Value = GetVSBrush(VsBrushes.AccentPaleKey)
                    }
                }
            },
            SeparatorStyle = new Style
            {
                TargetType = typeof(Line),
                Setters =
                {
                    new Setter
                    {
                        Property = Shape.StrokeProperty,
                        Value = GetVSBrush(VsBrushes.DebuggerDataTipActiveBorderKey)
                    }
                }
            }
        };

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

            if (!ExtensionPackage.LanguageClientInitialized.IsCompleted)
                return; // Language service is not available yet.

            string fileName = session.TextView.TextBuffer.GetFileName();
            if (fileName == null)
                return;

            SnapshotPoint? triggerPoint = session.GetTriggerPoint(session.TextView.TextSnapshot);
            if (!triggerPoint.HasValue)
                return;

            (int line, int column) = triggerPoint.Value.ToLineAndColumn();

            Hover hover = null;

            // A little bit of thread-fiddling required to do async in Visual Studio:
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await TaskScheduler.Default;

                try
                {
                    await ExtensionPackage.LanguageClientInitialized;

                    hover = await ExtensionPackage.LanguageClient.TextDocument.Hover(fileName, line, column);
                }
                catch (Exception hoverError)
                {
                    Trace.WriteLine(hoverError);
                }
            });

            if (hover == null)
                return;

            quickInfoContent.Clear();

            // Quick-and-dirty rendering of Markdown content for display in VS (WPF) UI:

            string hoverContent = String.Join("\n---\n", hover.Contents.Select(
                section => section.Value
            ));
            quickInfoContent.Add(new RichTextBox
            {
                Document = MarkdownRenderer.Transform(hoverContent),
                IsReadOnly = true,
                IsReadOnlyCaretVisible = false,
                MinWidth = 300,
                MinHeight = 50,
                Foreground = GetVSBrush(VsBrushes.HelpHowDoIPaneTextKey),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                Focusable = false
            });

            Span span = session.TextView.TextSnapshot.GetSpan(hover.Range);

            applicableToSpan = session.TextView.TextSnapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive);
        }

        /// <summary>
        ///     Get the specified <see cref="Brush"/> from Visual Studio application resources.
        /// </summary>
        /// <param name="brushKey">
        ///     An object from <see cref="VsBrushes"/> used as a resource key.
        /// </param>
        /// <returns>
        ///     The <see cref="Brush"/>, or <c>null</c> if no resource was found with the specified key.
        /// </returns>
        static Brush GetVSBrush(object brushKey) => (Brush)Application.Current.Resources[brushKey];
    }
}
