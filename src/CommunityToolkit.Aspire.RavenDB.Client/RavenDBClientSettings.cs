
using Raven.Client.Documents;
using System.Security.Cryptography.X509Certificates;

namespace CommunityToolkit.Aspire.RavenDB.Client;

/// <summary>
/// Provides the client configuration settings for connecting to a RavenDB database.
/// </summary>
public sealed class RavenDBClientSettings
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RavenDBClientSettings"/> class with the specified connection URLs and optional database name.
    /// </summary>
    /// <param name="urls">The URLs of the RavenDB server nodes.</param>
    /// <param name="databaseName">The optional name of the database to connect to.</param>
    public RavenDBClientSettings(string[] urls, string? databaseName = null)
    {
        ArgumentNullException.ThrowIfNull(urls);

        Urls = urls;
        DatabaseName = databaseName;
    }

    /// <summary>
    /// The URLs of the RavenDB server nodes.
    /// </summary>
    public string[] Urls { get; private set; }

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
    /// The name of the database to connect to.
    /// </summary>
    public string? DatabaseName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a new database should be created if it does not already exist.
    /// If set to <see langword="true"/> and a database with the specified name already exists, the existing database will be used.
    /// The default value is <see langword="false"/>.
    /// </summary>
    public bool CreateDatabase { get; init; } = false;

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
    /// Retrieves the <see cref="X509Certificate2"/> used for authentication, if a certificate path is specified.
    /// </summary>
    /// <returns>An <see cref="X509Certificate2"/> instance if the <see cref="CertificatePath"/> is specified;
    /// otherwise, <see langword="null"/>.</returns>
    internal X509Certificate2? GetCertificate()
    {
        if (Certificate != null)
            return Certificate;

        if (string.IsNullOrEmpty(CertificatePath))
        {
            return null;
        }

#pragma warning disable SYSLIB0057
        return new X509Certificate2(CertificatePath, CertificatePassword);
#pragma warning restore SYSLIB0057
    }
}
