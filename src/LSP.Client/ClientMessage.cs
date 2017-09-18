﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace LspClient
{
    /// <summary>
    ///     The client-side representation of an LSP message.
    /// </summary>
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class ClientMessage
    {
        /// <summary>
        ///     The JSON-RPC protocol version.
        /// </summary>
        [JsonProperty("jsonrpc")]
        public string ProtocolVersion => "2.0";

        /// <summary>
        ///     The request / response Id, if the message represents a request or a response.
        /// </summary>
        public object Id { get; set; }

        /// <summary>
        ///     The JSON-RPC method name.
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        ///     The request / notification message, if the message represents a request or a notification.
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public JObject Params { get; set; }

        /// <summary>
        ///     The response message, if the message represents a response.
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public JObject Result { get; set; }
    }
}
