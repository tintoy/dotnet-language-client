using System;
using System.IO;
using System.IO.Pipes;

namespace LSP.Client.Tests
{
    /// <summary>
    ///     Interconnected streams used to test language clients and servers.
    /// </summary>
    public struct TestStreams
        : IDisposable
    {
        /// <summary>
        ///     The streams used by the client (wired to <see cref="Server"/>).
        /// </summary>
        public InputOutputStreams Client { get; private set; }

        /// <summary>
        ///     The streams used by the client (wired to <see cref="Client"/>).
        /// </summary>
        public InputOutputStreams Server { get; private set; }

        /// <summary>
        ///     Dispose resources being used by the test streams.
        /// </summary>
        public void Dispose()
        {
            Client.Dispose();
            Server.Dispose();
        }

        /// <summary>
        ///     Create new test streams.
        /// </summary>
        /// <returns>
        ///     The new <see cref="TestStreams"/>.
        /// </returns>
        public static TestStreams Create()
        {
            AnonymousPipeServerStream serverInputStream = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None, bufferSize: 1024);
            AnonymousPipeServerStream serverOutputStream = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None, bufferSize: 1024);
            AnonymousPipeClientStream clientInputStream = new AnonymousPipeClientStream(PipeDirection.In, serverOutputStream.ClientSafePipeHandle);
            AnonymousPipeClientStream clientOutputStream = new AnonymousPipeClientStream(PipeDirection.Out, serverInputStream.ClientSafePipeHandle);

            return new TestStreams
            {
                Client = new InputOutputStreams(clientInputStream, clientOutputStream),
                Server = new InputOutputStreams(serverInputStream, serverOutputStream)
            };
        }

        /// <summary>
        ///     A pair of <see cref="Stream"/>s (one for input, one for output).
        /// </summary>
        public struct InputOutputStreams
            : IDisposable
        {
            /// <summary>
            ///     Create new <see cref="InputOutputStreams"/>.
            /// </summary>
            /// <param name="input">
            ///     The input stream.
            /// </param>
            /// <param name="output">
            ///     The output stream.
            /// </param>
            public InputOutputStreams(Stream input, Stream output)
            {
                Input = input;
                Output = output;
            }

            /// <summary>
            ///     Dispose of resources being used by the input and output streams.
            /// </summary>
            public void Dispose()
            {
                Input?.Dispose();
                Output?.Dispose();
            }

            /// <summary>
            ///     The input stream.
            /// </summary>
            public Stream Input { get; private set; }

            /// <summary>
            ///     The output stream.
            /// </summary>
            public Stream Output { get; private set; }
        }
    }
}
