using Lsp.Protocol;
using Serilog;
using Lsp.Capabilities.Client;
using Lsp.Models;
using System.Threading.Tasks;
using Lsp;

namespace Common
{
    /// <summary>
    ///     Handler for "workspace/didChangeConfiguration" notifications.
    /// </summary>
    class ConfigurationHandler
        : IDidChangeConfigurationHandler
    {
        public DidChangeConfigurationCapability Capability { get; private set; }

        public Task Handle(DidChangeConfigurationParams notification)
        {
            Log.Information("Received DidChangeConfiguration notification: {@Settings}", notification.Settings);

            return Task.CompletedTask;
        }

        void ICapability<DidChangeConfigurationCapability>.SetCapability(DidChangeConfigurationCapability capability)
        {
            Log.Information("ConfigurationHandler recieved capability: {@Capability}", capability);

            Capability = capability;
        }

        object IRegistration<object>.GetRegistrationOptions() => new object();
    }
}
