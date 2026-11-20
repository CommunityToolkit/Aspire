using Raven.Client.Documents;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace CommunityToolkit.Aspire.RavenDB.Client;

/// <summary>
/// Provides the client configuration settings for connecting to a RavenDB database.
/// </summary>
public sealed class RavenDBClientSettings
{
    /// <summary>
    /// The URLs of the RavenDB server nodes.
    /// </summary>
    public string[]? Urls { get; set; }

    /// <summary>
    /// The path to the certificate file.
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>
    /// The password for the certificate.
    /// </summary>
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// The certificate for RavenDB server.
    /// </summary>
    public X509Certificate2? Certificate { get; set; }

    /// <summary>
    /// The client certificate as base64-encoded PKCS #12 (.pfx) content, protected by <see cref="CertificatePassword"/>
    /// if it has one. Deployments that deliver the certificate as a value instead of a file, such as RavenDB Cloud
    /// through the Aspire hosting integration, set it through configuration
    /// (<c>Aspire:RavenDB:Client:{connection name}:CertificateBase64</c>).
    /// </summary>
    public string? CertificateBase64 { get; set; }

    /// <summary>
    /// The name of the database to connect to.
    /// </summary>
    public string? DatabaseName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a new database should be created if it does not already exist.
    /// If set to <see langword="true"/> and a database with the specified name already exists, the existing database will be used.
    /// Creating one needs a certificate with server-wide (Operator) access; checking that it exists does not.
    /// The default value is <see langword="false"/>.
    /// </summary>
    public bool CreateDatabase { get; set; }

    /// <summary>
    /// Action that allows modifications of the <see cref="IDocumentStore"/>.
    /// </summary>
    public Action<IDocumentStore>? ModifyDocumentStore { get; set; }

    /// <summary>
    /// Gets or sets a boolean value that indicates whether RavenDB health check is disabled or not.
    /// The default value is <see langword="false"/>.
    /// </summary>
    public bool DisableHealthChecks { get; set; }

    /// <summary>
    /// Gets or sets the timeout in milliseconds for the RavenDB health check.
    /// </summary>
    public int? HealthCheckTimeout { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether OpenTelemetry tracing is disabled.
    /// The default value is <see langword="false"/>.
    /// </summary>
    public bool DisableTracing { get; set; }

    /// <summary>
    /// Whether a client certificate is configured in any of the supported ways.
    /// </summary>
    internal bool HasCertificate =>
        Certificate != null || !string.IsNullOrEmpty(CertificateBase64) || !string.IsNullOrEmpty(CertificatePath);

    /// <summary>
    /// Retrieves the <see cref="X509Certificate2"/> used for authentication: <see cref="Certificate"/>, else the one in
    /// <see cref="CertificateBase64"/>, else the one at <see cref="CertificatePath"/>.
    /// </summary>
    /// <returns>The certificate, or <see langword="null"/> when none is configured.</returns>
    internal X509Certificate2? GetCertificate()
    {
        if (Certificate != null)
            return Certificate;

        if (!string.IsNullOrEmpty(CertificateBase64))
        {
            byte[] content;

            try
            {
                content = Convert.FromBase64String(CertificateBase64);
            }
            catch (FormatException)
            {
                // The value is a secret: never echo it.
                throw new InvalidOperationException(
                    "The RavenDB client certificate in 'CertificateBase64' is not valid base64. It has to be the base64-encoded content of a .pfx file.");
            }

            try
            {
#pragma warning disable SYSLIB0057
                return new X509Certificate2(content, CertificatePassword);
#pragma warning restore SYSLIB0057
            }
            catch (CryptographicException exception)
            {
                throw new InvalidOperationException(
                    $"The RavenDB client certificate in 'CertificateBase64' cannot be loaded: {exception.Message} It has to be the " +
                    "base64-encoded content of a .pfx file, with 'CertificatePassword' set if the file has a password.",
                    exception);
            }
        }

        if (string.IsNullOrEmpty(CertificatePath))
        {
            return null;
        }

#pragma warning disable SYSLIB0057
        return new X509Certificate2(CertificatePath, CertificatePassword);
#pragma warning restore SYSLIB0057
    }
}
