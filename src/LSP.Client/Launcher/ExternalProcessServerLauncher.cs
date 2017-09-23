using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LSP.Client.Launcher
{
    /// <summary>
    ///     An <see cref="ExternalProcessServerLauncher"/> is a <see cref="ServerLauncher"/> that launches its server as an external process.
    /// </summary>
    public class ExternalProcessServerLauncher
        : ServerLauncher
    {
        /// <summary>
        ///     A <see cref="ProcessStartInfo"/> that describes how to start the server.
        /// </summary>
        readonly ProcessStartInfo _serverStartInfo;

        /// <summary>
        ///     The current server process (if any).
        /// </summary>
        Process _serverProcess;

        /// <summary>
        ///     Create a new <see cref="ExternalProcessServerLauncher"/>.
        /// </summary>
        /// <param name="serverStartInfo">
        ///     A <see cref="ProcessStartInfo"/> that describes how to start the server.
        /// </param>
        public ExternalProcessServerLauncher(ProcessStartInfo serverStartInfo)
        {
            if (serverStartInfo == null)
                throw new ArgumentNullException(nameof(serverStartInfo));

            _serverStartInfo = serverStartInfo;
        }

        /// <summary>
        ///     Dispose of resources being used by the launcher.
        /// </summary>
        /// <param name="disposing">
        ///     Explicit disposal?
        /// </param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Process serverProcess = Interlocked.Exchange(ref _serverProcess, null);
                if (serverProcess != null)
                {
                    if (!serverProcess.HasExited)
                        serverProcess.Kill();

                    serverProcess.Dispose();
                }
            }
        }

        /// <summary>
        ///     Is the server running?
        /// </summary>
        public override bool IsRunning => !ServerExitCompletion.Task.IsCompleted;

        /// <summary>
        ///     The server's input stream.
        /// </summary>
        public override Stream InputStream => _serverProcess?.StandardInput?.BaseStream;

        /// <summary>
        ///     The server's output stream.
        /// </summary>
        public override Stream OutputStream => _serverProcess?.StandardOutput?.BaseStream;

        /// <summary>
        ///     Start or connect to the server.
        /// </summary>
        public override Task Start()
        {
            ServerExitCompletion = new TaskCompletionSource<object>();

            _serverStartInfo.CreateNoWindow = true;
            _serverStartInfo.UseShellExecute = false;
            _serverStartInfo.RedirectStandardInput = true;
            _serverStartInfo.RedirectStandardOutput = true;

            Process serverProcess = _serverProcess = new Process
            {
                StartInfo = _serverStartInfo,
                EnableRaisingEvents = true
            };
            serverProcess.Exited += ServerProcess_Exit;

            if (!serverProcess.Start())
                throw new InvalidOperationException("Failed to launch language server .");

            ServerStartCompletion.TrySetResult(null);

            return Task.CompletedTask;
        }

        /// <summary>
        ///     Stop or disconnect from the server.
        /// </summary>
        public override async Task Stop()
        {
            Process serverProcess = Interlocked.Exchange(ref _serverProcess, null);
            if (serverProcess != null && !serverProcess.HasExited)
                serverProcess.Kill();

            await ServerExitCompletion.Task;
        }

        /// <summary>
        ///     Called when the server process has exited.
        /// </summary>
        /// <param name="sender">
        ///     The event sender.
        /// </param>
        /// <param name="args">
        ///     The event arguments.
        /// </param>
        void ServerProcess_Exit(object sender, EventArgs args)
        {
            Log.Verbose("Server process has exited.");

            OnExited();
            ServerExitCompletion.TrySetResult(null);
            ServerStartCompletion = new TaskCompletionSource<object>();
        }
    }
}
