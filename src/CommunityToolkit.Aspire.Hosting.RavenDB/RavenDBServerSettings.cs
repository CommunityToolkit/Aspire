namespace CommunityToolkit.Aspire.Hosting.RavenDB;

/// <summary>
/// Represents the settings for configuring a RavenDB server resource.
/// </summary>
public class RavenDBServerSettings
{
    /// <summary>
    /// The internal URL for the RavenDB server.
    /// If not specified, the container resource will automatically assign a random URL.
    /// </summary>
    public string? ServerUrl { get; set; }

    /// <summary>
    /// The setup mode for the server. This determines whether the server is secured, uses Let's Encrypt, or is unsecured.
    /// </summary>
    public SetupMode SetupMode { get; private set; }

    /// <summary>
    /// Gets the licensing options configured for the server.
    /// </summary>
    public LicensingOptions? LicensingOptions { get; private set; }

    /// <summary>
    /// Optional port to expose for the HTTP endpoint (Studio / REST API).
    /// If not set, Aspire will automatically assign a free port on the host at runtime.
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Optional port to expose for the TCP endpoint (data / cluster traffic).
    /// If not set, the default 38888 is used.
    /// </summary>
    public int? TcpPort { get; set; }

    /// <summary>
    /// Protected constructor to allow inheritance but prevent direct instantiation.
    /// </summary>
    protected RavenDBServerSettings() { }

    /// <summary>
    /// Creates an unsecured RavenDB server settings object with default settings.
    /// </summary>
    public static RavenDBServerSettings Unsecured() => new RavenDBServerSettings { SetupMode = SetupMode.None };

    /// <summary>
    /// Creates a secured RavenDB server settings object with the specified configuration.
    /// </summary>
    /// <param name="domainUrl">The public domain URL for the server.</param>
    /// <param name="certificatePath">The path to the certificate file.</param>
    /// <param name="certificatePassword">The password for the certificate file, if required. Optional.</param>
    /// <param name="serverUrl">The optional server URL.</param>
    public static RavenDBServerSettings Secured(string domainUrl, string certificatePath,
        string? certificatePassword = null, string? serverUrl = null)
    {
        return new RavenDBSecuredServerSettings(certificatePath, certificatePassword, domainUrl)
        {
            SetupMode = SetupMode.Secured,
            ServerUrl = serverUrl
        };
    }

    /// <summary>
    /// Creates a secured RavenDB server settings object with the specified configuration.
    /// </summary>
    /// <param name="domainUrl">The public domain URL for the server.</param>
    /// <param name="certificatePath">The path to the certificate file.</param>
    /// <param name="certificatePassword">The password for the certificate file, if required. Optional.</param>
    /// <param name="serverUrl">The optional server URL.</param>
    public static RavenDBServerSettings SecuredWithLetsEncrypt(string domainUrl, string certificatePath,
        string? certificatePassword = null, string? serverUrl = null)
    {
        return new RavenDBSecuredServerSettings(certificatePath, certificatePassword, domainUrl)
        {
            SetupMode = SetupMode.LetsEncrypt,
            ServerUrl = serverUrl
        };
    }

    /// <summary>
    /// Configures licensing options for the RavenDB server.
    /// </summary>
    /// <param name="license">The license string for the RavenDB server.</param>
    /// <param name="eulaAccepted">Indicates whether the End User License Agreement (EULA) has been accepted. Defaults to <c>true</c>.</param>
    public void WithLicense(string license, bool eulaAccepted = true)
    {
        LicensingOptions = new LicensingOptions(license, eulaAccepted);
    }
}

/// <summary>
/// Represents secured settings for a RavenDB server, including certificate information and a public server URL.
/// </summary>
public sealed class RavenDBSecuredServerSettings(string certificatePath, string? certificatePassword, string publicServerUrl) : RavenDBServerSettings
{
    /// <summary>
    /// The path to the certificate file.
    /// </summary>
    public string CertificatePath { get; } = certificatePath;

    /// <summary>
    /// The password for the certificate file, if required.
    /// </summary>
    public string? CertificatePassword { get; } = certificatePassword;

    /// <summary>
    /// The public server URL (domain) that the secured RavenDB server will expose.
    /// </summary>
    public string PublicServerUrl { get; } = publicServerUrl;
}

/// <summary>
/// Represents the setup modes for configuring a RavenDB server.
/// </summary>
public enum SetupMode
{
    /// <summary>
    /// No specific setup mode is applied.
    /// </summary>
    None,

    /// <summary>
    /// The server is secured using Let's Encrypt.
    /// </summary>
    LetsEncrypt,

    /// <summary>
    /// The server is secured using a provided SSL certificate.
    /// </summary>
    Secured,

    /// <summary>
    /// The server is unsecured.
    /// </summary>
    Unsecured
}

/// <summary>
/// Represents licensing options for a RavenDB server.
/// </summary>
public sealed class LicensingOptions(string license, bool eulaAccepted = true)
{
    /// <summary>
    /// RavenDB license string.
    /// </summary>
    public string License { get; } = license;

    /// <summary>
    /// Indicates whether the End User License Agreement (EULA) has been accepted.
    /// Defaults to <c>true</c>.
    /// By setting <c>EulaAccepted=true</c>, you agree to the terms and conditions outlined at
    /// <a href="https://ravendb.net/legal">https://ravendb.net/legal</a>.
    /// </summary>
    public bool EulaAccepted { get; } = eulaAccepted;
}