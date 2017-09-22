using Lsp.Models;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using System;

namespace VisualStudioExtension
{
    /// <summary>
    ///     Extension methods for the VS API.
    /// </summary>
    static class VisualStudioApiExtensions
    {
        /// <summary>
        ///     Write a line of text to the output pane.
        /// </summary>
        /// <param name="outputPane">
        ///     The output window pane.
        /// </param>
        /// <param name="message">
        ///     The message to write.
        /// </param>
        public static void WriteLine(this IVsOutputWindowPane outputPane, string message)
        {
            if (outputPane == null)
                throw new ArgumentNullException(nameof(outputPane));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            outputPane.OutputString(message + "\n");
        }

        /// <summary>
        ///     Write a line of text to the output pane.
        /// </summary>
        /// <param name="outputPane">
        ///     The output window pane.
        /// </param>
        /// <param name="messageOrFormat">
        ///     The message or message-format specifier to write.
        /// </param>
        /// <param name="formatArguments">
        ///     Optional message-format arguments.
        /// </param>
        public static void WriteLine(this IVsOutputWindowPane outputPane, string messageOrFormat, params object[] formatArguments)
        {
            if (outputPane == null)
                throw new ArgumentNullException(nameof(outputPane));

            if (string.IsNullOrWhiteSpace(messageOrFormat))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(messageOrFormat)}.", nameof(messageOrFormat));
            
            outputPane.WriteLine(
                String.Format(messageOrFormat, formatArguments)
            );
        }

        /// <summary>
        ///     Attempt to determine the name of the file represented by the specified text buffer.
        /// </summary>
        /// <param name="textBuffer">
        ///     The text buffer.
        /// </param>
        /// <returns>
        ///     The file name, or <c>null</c> if the file name could not be determined.
        /// </returns>
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

        /// <summary>
        ///     Get a <see cref="SnapshotPoint"/> representing the specified position in the <paramref name="snapshot"/>.
        /// </summary>
        /// <param name="snapshot">
        ///     The <see cref="ITextSnapshot"/>.
        /// </param>
        /// <param name="position">
        ///     The target LSP <see cref="Position"/>.
        /// </param>
        /// <returns>
        ///     The <see cref="SnapshotPoint"/>.
        /// </returns>
        public static SnapshotPoint GetPoint(this ITextSnapshot snapshot, Position position)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            if (position == null)
                throw new ArgumentNullException(nameof(position));

            return snapshot.GetPoint(position.Line, position.Character);
        }

        /// <summary>
        ///     Get a <see cref="SnapshotPoint"/> representing the specified (0-based) line and column in the <paramref name="snapshot"/>.
        /// </summary>
        /// <param name="snapshot">
        ///     The <see cref="ITextSnapshot"/>.
        /// </param>
        /// <param name="line">
        ///     The target line (0-based).
        /// </param>
        /// <param name="column">
        ///     The target column (0-based).
        /// </param>
        /// <returns>
        ///     The <see cref="SnapshotPoint"/>.
        /// </returns>
        public static SnapshotPoint GetPoint(this ITextSnapshot snapshot, long line, long column)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            ITextSnapshotLine snapshotLine = snapshot.GetLineFromLineNumber((int)line);

            return snapshotLine.Start.Add((int)column);
        }

        /// <summary>
        ///     Convert the <see cref="SnapshotPoint"/> to a (0-based) line and column number.
        /// </summary>
        /// <param name="snapshotPoint">
        ///     The <see cref="SnapshotPoint"/>.
        /// </param>
        /// <returns>
        ///     The line and column number.
        /// </returns>
        public static (int line, int column) ToLineAndColumn(this SnapshotPoint snapshotPoint)
        {
            var line = snapshotPoint.GetContainingLine();
            int lineNumber = line.LineNumber;
            int columnNumber = snapshotPoint.Subtract(line.Start).Position;

            return (lineNumber, columnNumber);
        }

        /// <summary>
        ///     Get a <see cref="Span"/> representing the specified range within the <paramref name="snapshot"/>.
        /// </summary>
        /// <param name="snapshot">
        ///     The <see cref="ITextSnapshot"/>.
        /// </param>
        /// <param name="range">
        ///     The target LSP <see cref="Range"/>.
        /// </param>
        /// <returns>
        ///     The <see cref="Span"/>.
        /// </returns>
        public static Span GetSpan(this ITextSnapshot snapshot, Range range)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            if (range == null)
                throw new ArgumentNullException(nameof(range));

            SnapshotPoint start = snapshot.GetPoint(range.Start);
            SnapshotPoint end = snapshot.GetPoint(range.End);

            return new Span(
                start: start.Position,
                length: end.Position - start.Position
            );
        }
    }
}
