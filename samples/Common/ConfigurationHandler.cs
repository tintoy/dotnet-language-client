using OmniSharp.Extensions.LanguageServer.Abstractions;
using OmniSharp.Extensions.LanguageServer.Capabilities.Client;
using OmniSharp.Extensions.LanguageServer.Models;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Serilog;
using System.Threading.Tasks;

namespace Common
{
    /// <summary>
    ///     Handler for "workspace/didChangeConfiguration" notifications.
    /// </summary>
    public class ConfigurationHandler
        : IDidChangeConfigurationHandler
    {
        /// <summary>
        ///     The client-side capabilities for DidChangeConfiguration.
        /// </summary>
        public DidChangeConfigurationCapability Capabilities { get; private set; }

        /// <summary>
        ///     Handle a "workspace/didChangeConfiguration" notification.
        /// </summary>
        /// <param name="notification">
        ///     The notification message.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        public Task Handle(DidChangeConfigurationParams notification)
        {
            Log.Information("Received DidChangeConfiguration notification: {@Settings}", notification.Settings);

            return Task.CompletedTask;
        }

        /// <summary>
        ///     Called to notify the handler of the client-side capabilities for DidChangeconfiguration.
        /// </summary>
        /// <param name="capabilities">
        ///     A <see cref="DidChangeConfigurationCapability"/> representing the capabilities.
        /// </param>
        void ICapability<DidChangeConfigurationCapability>.SetCapability(DidChangeConfigurationCapability capabilities)
        {
            Log.Information("ConfigurationHandler recieved capability: {@Capability}", capabilities);

            Capabilities = capabilities;
        }

        /// <summary>
        ///     Get registration options (unused).
        /// </summary>
        /// <returns>
        ///     <c>null</c>
        /// </returns>
        object IRegistration<object>.GetRegistrationOptions() => null;
    }
}
