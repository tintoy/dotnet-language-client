using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace LSP.Client.Tests
{
    using Dispatcher;
    using Protocol;

    /// <summary>
    ///     Tests for <see cref="LspConnection"/>.
    /// </summary>
    public class ConnectionTests
        : PipeServerTestBase
    {
        /// <summary>
        ///     Create a new <see cref="LspConnection"/> test suite.
        /// </summary>
        /// <param name="testOutput"></param>
        public ConnectionTests(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        /// <summary>
        ///     Verify that 2 <see cref="LspConnection"/>s can be connected together.
        /// </summary>
        [Fact(DisplayName = "Two LspConnections can be connected together")]
        public async Task Connection_To_Connection_Connect_Success()
        {
            LspDispatcher serverDispatcher = new LspDispatcher();
            serverDispatcher.HandleEmptyNotification("test", () =>
            {
                Log.Information("Yep!");
            });

            LspConnection serverConnection = await CreateServerConnection();
            serverConnection.Open(serverDispatcher);

            LspConnection clientConnection = await CreateClientConnection();
            clientConnection.Open(new LspDispatcher());
            clientConnection.SendEmptyNotification("test");

            clientConnection.Close(flushOutgoing: true);
            serverConnection.Close();

            await Task.WhenAll(clientConnection.HasClosed, serverConnection.HasClosed);
        }
    }
}
