using LSP.Client;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Serilog;
using Serilog.Events;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;
using System.Threading;
using Lsp.Models;

namespace VisualStudioExtension
{
    /// <summary>
    ///     The visual studio extension package.
    /// </summary>
    [Guid(PackageGuidString)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists)]
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    public sealed class ExtensionPackage
        : AsyncPackage
    {
        /// <summary>
        ///     The package GUID, as a string.
        /// </summary>
        public const string PackageGuidString = "bfe31c89-943f-4106-ad20-5c60f656e9be";

        /// <summary>
        ///     The GUID of the package's output window pane.
        /// </summary>
        public static readonly Guid PackageOutputPaneGuid = new Guid("9d7abb60-bbe9-4e72-95ff-8cf6df23d5f9");

        static readonly TaskCompletionSource<object> InitCompletion = new TaskCompletionSource<object>();

        static ExtensionPackage()
        {
            LanguageClientInitialized = InitCompletion.Task;
        }

        /// <summary>
        ///     Create a new <see cref="ExtensionPackage"/>.
        /// </summary>
        public ExtensionPackage()
        {
            Trace.WriteLine("Enter ExtensionPackage constructor.");

            try
            {
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Verbose()
                    .WriteTo.Debug(
                        restrictedToMinimumLevel: LogEventLevel.Information
                    )
                    .CreateLogger();
            }
            finally
            {
                Trace.WriteLine("Exit ExtensionPackage constructor.");
            }
        }

        /// <summary>
        ///     The package's output window pane.
        /// </summary>
        public static IVsOutputWindowPane OutputPane { get; private set; }

        /// <summary>
        ///     The LSP client.
        /// </summary>
        public static LanguageClient LanguageClient { get; private set; }

        /// <summary>
        ///     A <see cref="Task"/> representing language client initialisation.
        /// </summary>
        public static Task LanguageClientInitialized { get; private set; }

        /// <summary>
        ///     Dispose of resources being used by the extension package.
        /// </summary>
        /// <param name="disposing">
        ///     Explicit disposal?
        /// </param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (LanguageClient != null)
                {
                    LanguageClient.Dispose();
                    LanguageClient = null;
                }
            }

            base.Dispose(disposing);
        }

        /// <summary>
        ///     Called when the package is initialising.
        /// </summary>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <param name="progress">
        ///     The initialisation progress-reporting facility.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing package initialisation.
        /// </returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            Trace.WriteLine("Enter ExtensionPackage.InitializeAsync.");

            cancellationToken.Register(
                () => InitCompletion.TrySetCanceled(cancellationToken)
            );

            await base.InitializeAsync(cancellationToken, progress);

            OutputPane = GetOutputPane(PackageOutputPaneGuid, "LSP Demo");
            OutputPane.Activate();

            await TaskScheduler.Default;

            try
            {
                Trace.WriteLine("Creating language service...");

                LanguageClient = new LanguageClient(Log.Logger, new ProcessStartInfo("dotnet")
                {
                    Arguments = @"""D:\Development\github\tintoy\msbuild-project-tools\out\language-server\MSBuildProjectTools.LanguageServer.Host.dll""",
                    //Arguments = @"""D:\Development\github\tintoy\dotnet-language-client\samples\Server\bin\Debug\netcoreapp2.0\Server.dll""",
                    Environment =
                    {
                        ["MSBUILD_PROJECT_TOOLS_DIR"] = @"D:\Development\github\tintoy\msbuild-project-tools",
                        ["MSBUILD_PROJECT_TOOLS_SEQ_URL"] = "http://localhost:5341/",
                        ["MSBUILD_PROJECT_TOOLS_SEQ_API_KEY"] = "wxEURGakoVuXpIRXyMnt",
                        ["MSBUILD_PROJECT_TOOLS_VERBOSE_LOGGING"] = "1",
                        ["LSP_SEQ_URL"] = "http://localhost:5341/",
                        ["LSP_SEQ_API_KEY"] = "wxEURGakoVuXpIRXyMnt",
                        ["LSP_VERBOSE_LOGGING"] = "1"
                    }
                });
                LanguageClient.Window.OnLogMessage(LanguageClient_LogMessage);

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                Trace.WriteLine("Retrieving solution directory...");

                IVsSolution solution = (IVsSolution)GetService(typeof(SVsSolution));

                int hr = solution.GetSolutionInfo(out string solutionDir, out _, out _);
                ErrorHandler.ThrowOnFailure(hr);

                Trace.WriteLine("Initialising language client...");

                await TaskScheduler.Default;

                await LanguageClient.Initialize(solutionDir);

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                Trace.WriteLine("Language client initialised.");

                InitCompletion.TrySetResult(null);
            }
            catch (Exception languageClientError)
            {
                Trace.WriteLine(languageClientError);
                
                InitCompletion.TrySetException(languageClientError);
            }
            finally
            {
                Trace.WriteLine("Exit ExtensionPackage.InitializeAsync.");
            }
        }

        /// <summary>
        ///     Called when the language client receives a log message from the language server.
        /// </summary>
        /// <param name="message">
        ///     The message text.
        /// </param>
        /// <param name="messageType">
        ///     The message type.
        /// </param>
        async void LanguageClient_LogMessage(string message, MessageType messageType)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            OutputPane.WriteLine("[{0}] {1}", messageType, message);
        }
    }
}
