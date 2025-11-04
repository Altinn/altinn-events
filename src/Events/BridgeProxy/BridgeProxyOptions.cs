namespace Altinn.Platform.Events.BridgeProxy
{
    /// <summary>
    /// Configuration options for the BridgeProxy, including target system base address,
    /// timeout for outbound calls, and header forwarding behavior.
    /// </summary>
    public class BridgeProxyOptions
    {
        /// <summary>
        /// Gets or sets the base URL of the target system (no trailing slash).
        /// Example: https://target-host:5005
        /// </summary>
        public string BaseAddress { get; set; } = "https://ai-yt01-vip-sblbridge.ai.basefarm.net/";

        /// <summary>
        /// Gets or sets the timeout in seconds for outbound calls.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 100;

        /// <summary>
        /// Gets or sets a value indicating whether to copy all inbound headers except Host.
        /// If false, only a safe subset of headers will be forwarded.
        /// </summary>
        public bool ForwardAllHeaders { get; set; } = true;
    }
}
