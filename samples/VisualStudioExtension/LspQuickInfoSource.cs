﻿using Lsp.Models;
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

            string fileName = session.TextView.TextBuffer.GetFileName();
            if (fileName == null)
                return;

            SnapshotPoint? triggerPoint = session.GetTriggerPoint(session.TextView.TextSnapshot);
            if (!triggerPoint.HasValue)
                return;

            (int line, int column) = triggerPoint.Value.GetLineAndColumn();

            Hover hover = null;

            // A little bit of thread-fiddling required to do async in Visual Studio:
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await TaskScheduler.Default;

                try
                {
                    Trace.WriteLine("Awaiting ExtensionPackage.LanguageClientInitializeTask...");

                    await ExtensionPackage.LanguageClientInitialized;

                    Trace.WriteLine("Calling ExtensionPackage.LanguageClient.TextDocument.Hover...");

                    hover = await ExtensionPackage.LanguageClient.TextDocument.Hover(fileName, line, column);

                    Trace.WriteLine("Called ExtensionPackage.LanguageClient.TextDocument.Hover.");
                }
                catch (Exception hoverError)
                {
                    Trace.WriteLine(hoverError);
                }
            });

            if (hover == null)
            {
                Trace.WriteLine("No hover provided.");

                return;
            }

            Trace.WriteLine("Hover provided by language service.");

            quickInfoContent.Clear();

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

            Trace.WriteLine("LspQuickInfoSource.AugmentQuickInfoSession complete.");
        }

        static Brush GetVSBrush(object brushKey) => (Brush)Application.Current.Resources[brushKey];
    }

    static class Extensions
    {
        public static SnapshotPoint GetPointInLine(this ITextSnapshot snapshot, long line, long column)
        {
            var snapshotLine = snapshot.GetLineFromLineNumber((int)line);
            return snapshotLine.Start.Add((int)column);
        }

        public static (int line, int column) GetLineAndColumn(this SnapshotPoint snapshotPoint)
        {
            var line = snapshotPoint.GetContainingLine();
            int lineNumber = line.LineNumber;
            int columnNumber = snapshotPoint.Subtract(line.Start).Position;

            return (lineNumber, columnNumber);
        }

        public static Span GetSpan(this ITextSnapshot textSnapshot, Range range)
        {
            SnapshotPoint start = textSnapshot.GetPointInLine(range.Start.Line, range.Start.Character);
            SnapshotPoint end = textSnapshot.GetPointInLine(range.End.Line, range.End.Character);

            return new Span(
                start: start.Position,
                length: end.Position - start.Position
            );
        }

        public static string GetFileName(this ITextBuffer textBuffer)
        {
            if (textBuffer == null)
                throw new ArgumentNullException(nameof(textBuffer));

            IVsTextBuffer bufferAdapter;
            textBuffer.Properties.TryGetProperty(typeof(IVsTextBuffer), out bufferAdapter);
            if (bufferAdapter == null)
                return null;

            IPersistFileFormat persistFileFormat = bufferAdapter as IPersistFileFormat;
            if (persistFileFormat == null)
                return null;

            string fileName = null;

            int hr = persistFileFormat.GetCurFile(out fileName, out _);
            ErrorHandler.ThrowOnFailure(hr);

            return fileName;
        }
    }
}
