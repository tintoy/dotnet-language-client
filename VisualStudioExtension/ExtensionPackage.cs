using LSP.Client;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Task = System.Threading.Tasks.Task;

namespace VisualStudioExtension
{
    /// <summary>
    ///     The visual studio extension package.
    /// </summary>
    [Guid(PackageGuidString)]
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    public sealed class ExtensionPackage
        : Package
    {
        /// <summary>
        ///     The package GUID, as a string.
        /// </summary>
        public const string PackageGuidString = "bfe31c89-943f-4106-ad20-5c60f656e9be";

        /// <summary>
        ///     Create a new <see cref="ExtensionPackage"/>.
        /// </summary>
        public ExtensionPackage()
        {
            LanguageClient = new LanguageClient(new ProcessStartInfo("dotnet")
            {
                Arguments = @"""D:\Development\github\tintoy\msbuild-project-tools\out\language-server\MSBuildProjectTools.LanguageServer.Host.dll""",
                Environment =
                {
                    ["MSBUILD_PROJECT_TOOLS_DIR"] = @"D:\Development\github\tintoy\msbuild-project-tools",
                    ["MSBUILD_PROJECT_TOOLS_SEQ_URL"] = "http://localhost:5341/",
                    ["MSBUILD_PROJECT_TOOLS_SEQ_API_KEY"] = "wxEURGakoVuXpIRXyMnt"
                }
            });
        }

        /// <summary>
        ///     The LSP client.
        /// </summary>
        public static LanguageClient LanguageClient { get; private set; }

        /// <summary>
        ///     A <see cref="Task"/> representing language client initialisation.
        /// </summary>
        public static Task LanguageClientInitializeTask { get; private set; }

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
        protected override void Initialize()
        {
            base.Initialize();

            IVsSolution solution = (IVsSolution)GetService(typeof(SVsSolution));

            int hr = solution.GetSolutionInfo(out string solutionDir, out _, out _);
            ErrorHandler.ThrowOnFailure(hr);

            // TODO: Configure Serilog.

            LanguageClientInitializeTask = LanguageClient.Initialize(solutionDir);
        }        
    }
}
