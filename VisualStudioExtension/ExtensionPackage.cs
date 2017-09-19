using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace VisualStudioExtension
{
    /// <summary>
    ///     The visual studio extension package.
    /// </summary>
    [Guid(PackageGuidString)]
    [PackageRegistration(UseManagedResourcesOnly = true)]
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
        }

        /// <summary>
        ///     Called when the package is initialising.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();
        }        
    }
}
