namespace CommunityToolkit.Aspire.Hosting.Floci;

/// <summary>
/// The env var names a Floci image reads to configure its TLS listener. Each image uses the same
/// four settings under its own prefix (<c>FLOCI_TLS_*</c> for AWS, <c>FLOCI_AZ_TLS_*</c> for Azure),
/// so mapping Aspire's provisioned certificate onto them only needs the names.
/// </summary>
/// <param name="Enabled">Env var that turns the TLS listener on.</param>
/// <param name="CertPath">Env var holding the container-side path to a PEM certificate.</param>
/// <param name="KeyPath">Env var holding the container-side path to a PEM private key.</param>
/// <param name="SelfSigned">Env var that enables runtime self-signed certificate generation.</param>
internal sealed record FlociTlsEnvVars(
    string Enabled,
    string CertPath,
    string KeyPath,
    string SelfSigned);
