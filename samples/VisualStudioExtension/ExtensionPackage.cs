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
        /// <param name="cancellationToken"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            Trace.WriteLine("Enter ExtensionPackage.InitializeAsync.");

            await base.InitializeAsync(cancellationToken, progress);

            await TaskScheduler.Default;

            try
            {
                Trace.WriteLine("Creating language service...");

                LanguageClient = new LanguageClient(new ProcessStartInfo("dotnet")
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

                Trace.WriteLine("Retrieving solution directory...");

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                IVsSolution solution = (IVsSolution)GetService(typeof(SVsSolution));

                int hr = solution.GetSolutionInfo(out string solutionDir, out _, out _);
                Trace.WriteLine($"GetSolutionInfo: hr = {hr}");
                ErrorHandler.ThrowOnFailure(hr);

                await TaskScheduler.Default;

                Trace.WriteLine("Initialising language client...");

                await LanguageClient.Initialize(solutionDir);

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
    }
}
