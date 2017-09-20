using Microsoft.VisualStudio.Shell;

[assembly: ProvideCodeBase(AssemblyName = "System.Net.Http", PublicKeyToken = "b03f5f7f11d50a3a", CodeBase = @"$PackageFolder$\System.Net.Http.dll")]
[assembly: ProvideCodeBase(AssemblyName = "netstandard", PublicKeyToken = "cc7b13ffcd2ddd51", CodeBase = @"$PackageFolder$\netstandard.dll")]

[assembly: ProvideCodeBase(AssemblyName = "JsonRpc", CodeBase = @"$PackageFolder$\JsonRpc.dll")]
[assembly: ProvideCodeBase(AssemblyName = "Lsp", CodeBase = @"$PackageFolder$\Lsp.dll")]

[assembly: ProvideCodeBase(AssemblyName = "Serilog", PublicKeyToken = "24c2f752a8e58a10", CodeBase = @"$PackageFolder$\Serilog.dll")]
[assembly: ProvideCodeBase(AssemblyName = "Serilog.Sinks.Debug", PublicKeyToken = "24c2f752a8e58a10", CodeBase = @"$PackageFolder$\Serilog.Sinks.Debug.dll")]
[assembly: ProvideCodeBase(AssemblyName = "Serilog.Sinks.File", PublicKeyToken = "24c2f752a8e58a10", CodeBase = @"$PackageFolder$\Serilog.Sinks.File.dll")]
[assembly: ProvideCodeBase(AssemblyName = "Serilog.Sinks.PeriodicBatching", PublicKeyToken = "24c2f752a8e58a10", CodeBase = @"$PackageFolder$\Serilog.Sinks.PeriodicBatching.dll")]
[assembly: ProvideCodeBase(AssemblyName = "Serilog.Sinks.RollingFile", PublicKeyToken = "24c2f752a8e58a10", CodeBase = @"$PackageFolder$\Serilog.Sinks.RollingFile.dll")]
[assembly: ProvideCodeBase(AssemblyName = "Serilog.Sinks.Seq", PublicKeyToken = "24c2f752a8e58a10", CodeBase = @"$PackageFolder$\Serilog.Sinks.Seq.dll")]

[assembly: ProvideCodeBase(AssemblyName = "System.Diagnostics.Process", PublicKeyToken = "b03f5f7f11d50a3a", CodeBase = @"$PackageFolder$\System.Diagnostics.Process.dll")]
[assembly: ProvideBindingRedirection(AssemblyName = "System.Diagnostics.Process", OldVersionLowerBound = "4.0.0.0", OldVersionUpperBound = "4.1.2.0", NewVersion = "4.1.2.0")]

[assembly: ProvideCodeBase(AssemblyName = "System.IO", PublicKeyToken = "b03f5f7f11d50a3a", CodeBase = @"$PackageFolder$\System.IO.dll")]
[assembly: ProvideBindingRedirection(AssemblyName = "System.IO", OldVersionLowerBound = "4.0.0.0", OldVersionUpperBound = "4.1.2.0", NewVersion = "4.1.2.0")]

[assembly: ProvideCodeBase(AssemblyName = "System.Runtime", PublicKeyToken = "b03f5f7f11d50a3a", CodeBase = @"$PackageFolder$\System.Runtime.dll")]
[assembly: ProvideBindingRedirection(AssemblyName = "System.Runtime", OldVersionLowerBound = "4.0.0.0", OldVersionUpperBound = "4.1.2.0", NewVersion = "4.1.2.0")]

[assembly: ProvideCodeBase(AssemblyName = "System.ValueTuple", PublicKeyToken = "b03f5f7f11d50a3a", CodeBase = @"$PackageFolder$\System.ValueTuple.dll")]
[assembly: ProvideBindingRedirection(AssemblyName = "System.ValueTuple", OldVersionLowerBound = "4.0.0.0", OldVersionUpperBound = "4.0.2.0", NewVersion = "4.0.2.0")]
