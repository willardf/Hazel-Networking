namespace Hazel.Udp
{
    /// <summary>
    /// Version/capabilities byte carried as the first byte of the UDP Hello payload.
    /// This byte is not exposed to application-level handshake payload (it is stripped by listeners).
    /// </summary>
    public enum HazelHelloVersion : byte
    {
        /// <summary>
        /// Legacy Hazel - no UDP fragmentation/MTU discovery support.
        /// </summary>
        Legacy = 0,

        /// <summary>
        /// Supports UDP fragmentation (UdpSendOption.Fragment) and MTU discovery (UdpSendOption.MtuTest).
        /// </summary>
        Fragmentation = 1,
    }
}
