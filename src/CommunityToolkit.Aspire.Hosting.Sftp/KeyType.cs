namespace Aspire.Hosting;

/// <summary>
/// Specifies the types of cryptographic keys supported for digital signatures.
/// </summary>
public enum KeyType
{
    /// <summary>
    /// Specifies the Ed25519 public-key signature algorithm.
    /// </summary>
    Ed25519,

    /// <summary>
    /// Specifies the RSA public-key signature algorithm.
    /// </summary>
    Rsa
}
