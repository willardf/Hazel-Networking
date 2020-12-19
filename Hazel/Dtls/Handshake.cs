namespace Hazel.Dtls
{
    /// <summary>
    /// Named curves
    /// </summary>
    public enum NamedCurve : ushort
    {
        Reserved = 0,
        secp256r1 = 23,
        x25519 = 29,
    }

    /// <summary>
    /// Elliptic curve type
    /// </summary>
    public enum ECCurveType : byte
    {
        NamedCurve = 3,
    }
}
