using Microsoft.VisualStudio.Shell;

// Register paths for our dependencies to ensure Visual Studio can find them when it loads our extension.

[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\OmniSharp.Extensions.JsonRpc.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\OmniSharp.Extensions.LanguageServerProtocol.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Serilog.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Serilog.Sinks.Debug.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Serilog.Sinks.File.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Serilog.Sinks.PeriodicBatching.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Serilog.Sinks.RollingFile.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\Serilog.Sinks.Seq.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\System.Diagnostics.Process.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\System.IO.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\System.Runtime.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\System.Reactive.Core.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\System.Reactive.Interfaces.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\System.Reactive.Linq.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\System.Reactive.PlatformServices.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\System.Reactive.Windows.Threading.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\System.ValueTuple.dll")]
