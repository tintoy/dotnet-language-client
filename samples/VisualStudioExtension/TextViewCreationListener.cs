using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace VisualStudioExtension
{
    /// <summary>
    ///     Listens for creation of text views with the XML content type, and notifies the language client (via TextDocument.DidOpen).
    /// </summary>
    [ContentType("xml")]
    [Export(typeof(IVsTextViewCreationListener))]
    [Name("token completion handler")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    class XmlTextViewCreationListener
        : IVsTextViewCreationListener
    {
        /// <summary>
        ///     The editor adapter factory service.
        /// </summary>
        [Import]
        public IVsEditorAdaptersFactoryService EditorAdapters { get; set; }

        /// <summary>
        ///     Called when a text view is created.
        /// </summary>
        /// <param name="textViewAdapter">
        ///     An <see cref="IVsTextView"/> representing the text view.
        /// </param>
        public async void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            if (textViewAdapter == null)
                return;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            IWpfTextView textView = EditorAdapters.GetWpfTextView(textViewAdapter);
            if (textView == null)
                return;

            ITextBuffer buffer = textView.TextBuffer;
            if (buffer == null)
                return;

            string contentType = buffer.ContentType.TypeName;

            string fileName = buffer.GetFileName();
            if (String.IsNullOrWhiteSpace(fileName))
                return;

            textView.Closed += async (sender, args) =>
            {
                await ExtensionPackage.LanguageClientInitialized;

                ExtensionPackage.LanguageClient.TextDocument.DidClose(fileName);
            };

            await TaskScheduler.Default;
            await ExtensionPackage.LanguageClientInitialized;

            ExtensionPackage.LanguageClient.TextDocument.DidOpen(fileName, languageId: contentType.ToLower());
        }
    }
}
