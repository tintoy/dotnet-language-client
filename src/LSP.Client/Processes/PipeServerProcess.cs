using Serilog;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace LSP.Client.Processes
{
    /// <summary>
    ///     An <see cref="PipeServerProcess"/> is a <see cref="ServerProcess"/> that creates anonymous pipe streams to connect a language client to a language server in the same process.
    /// </summary>
    public class PipeServerProcess
        : ServerProcess
    {
        /// <summary>
        ///     Create a new <see cref="PipeServerProcess"/>.
        /// </summary>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        public PipeServerProcess(ILogger logger)
            : base(logger)
        {
        }

        /// <summary>
        ///     Dispose of resources being used by the <see cref="PipeServerProcess"/>.
        /// </summary>
        /// <param name="disposing">
        ///     Explicit disposal?
        /// </param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                CloseStreams();

            base.Dispose(disposing);
        }


        /// <summary>
        ///     Is the server running?
        /// </summary>
        public override bool IsRunning { get; }

        /// <summary>
        ///     An <see cref="AnonymousPipeClientStream"/> that the client reads messages from.
        /// </summary>
        public AnonymousPipeClientStream ClientInputStream { get; protected set; }

        /// <summary>
        ///     An <see cref="AnonymousPipeClientStream"/> that the client writes messages to.
        /// </summary>
        public AnonymousPipeClientStream ClientOutputStream { get; protected set; }

        /// <summary>
        ///     An <see cref="AnonymousPipeServerStream"/> that the server reads messages from.
        /// </summary>
        public AnonymousPipeServerStream ServerInputStream { get; protected set; }

        /// <summary>
        ///     An <see cref="AnonymousPipeServerStream"/> that the server writes messages to.
        /// </summary>
        public AnonymousPipeServerStream ServerOutputStream { get; protected set; }

        /// <summary>
        ///     The server's input stream.
        /// </summary>
        public override Stream InputStream => ServerInputStream;

        /// <summary>
        ///     The server's output stream.
        /// </summary>
        public override Stream OutputStream => ServerOutputStream;

        /// <summary>
        ///     Start or connect to the server.
        /// </summary>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        public override Task Start()
        {
            ServerExitCompletion = new TaskCompletionSource<object>();

            ServerInputStream = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None, bufferSize: 1024);
            ServerOutputStream = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None, bufferSize: 1024);
            ClientInputStream = new AnonymousPipeClientStream(PipeDirection.Out, ServerOutputStream.ClientSafePipeHandle);
            ClientOutputStream = new AnonymousPipeClientStream(PipeDirection.In, ServerInputStream.ClientSafePipeHandle);

            ServerStartCompletion.TrySetResult(null);

            return Task.CompletedTask;
        }

        /// <summary>
        ///     Stop the server.
        /// </summary>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        public override Task Stop()
        {
            ServerStartCompletion = new TaskCompletionSource<object>();

            CloseStreams();

            ServerExitCompletion.TrySetResult(null);

            return Task.CompletedTask;
        }

        /// <summary>
        ///     Close the underlying streams.
        /// </summary>
        void CloseStreams()
        {
            ClientInputStream?.Dispose();
            ClientInputStream = null;

            ClientOutputStream?.Dispose();
            ClientOutputStream = null;

            ServerInputStream?.Dispose();
            ServerInputStream = null;

            ServerOutputStream?.Dispose();
            ServerOutputStream = null;
        }
    }
}
