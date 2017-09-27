using System;
using System.IO.Pipes;
using System.Threading.Tasks;
using Xunit;
using System.Threading;
using Xunit.Abstractions;

namespace LSP.Client.Tests
{
    public class PipeTests
        : TestBase
    {
        public PipeTests(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        [Fact]
        public async Task Temp()
        {
            NamedPipeServerStream source = new NamedPipeServerStream("pipe-test", PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            NamedPipeClientStream destination = new NamedPipeClientStream(".", "pipe-test", PipeDirection.In, PipeOptions.Asynchronous);
            await destination.ConnectAsync();

            CancellationTokenSource cancellationSource = new CancellationTokenSource();

            byte[] buffer = new byte[10];
            Task<int> readTask = destination.ReadAsync(buffer, 0, buffer.Length, cancellationSource.Token);

            Task timeout = Task.Delay(TimeSpan.FromSeconds(1));
            Task winner = await Task.WhenAny(readTask, timeout);
            Assert.Equal(timeout.Id, winner.Id);

            cancellationSource.Cancel();

            timeout = Task.Delay(TimeSpan.FromSeconds(5));

            winner = await Task.WhenAny(readTask, timeout);
            Assert.Equal(readTask.Id, winner.Id);

            TaskCanceledException cancelled = await Assert.ThrowsAsync<TaskCanceledException>(() => readTask);
            Assert.Equal(cancellationSource.Token, cancelled.CancellationToken);
        }
    }
}
