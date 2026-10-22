using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.RavenDB.Cloud;

/// <summary>Whether the AppHost owns the product or uses one that exists.</summary>
internal enum RavenDBCloudMode
{
    /// <summary><c>PublishAsRavenDBCloud</c>: the deployment creates the product and keeps it to its specification.</summary>
    Managed,

    /// <summary><c>PublishAsExistingRavenDBCloud</c>: the product exists; the deployment never creates, changes or terminates it.</summary>
    Attached,
}

/// <summary>
/// Attached to a <see cref="RavenDBServerResource"/> published to RavenDB Cloud: what the AppHost asks for and, while
/// the deployment runs, what it found.
/// </summary>
internal sealed class RavenDBCloudDeployment : IResourceAnnotation
{
    /// <summary>The longest subdomain RavenDB Cloud accepts.</summary>
    public const int MaxSubdomainLength = 13;

    /// <summary>The longest product name RavenDB Cloud accepts.</summary>
    public const int MaxProductNameLength = 100;

    public RavenDBCloudDeployment(
        RavenDBServerResource server,
        ParameterResource apiKey,
        RavenDBCloudMode mode,
        string productName,
        RavenDBCloudSpec? spec,
        string? clientName,
        RavenDBCloudOptions options)
    {
        Server = server;
        ApiKey = apiKey;
        Mode = mode;
        ProductName = productName;
        Spec = spec;
        ClientName = clientName;
        Options = options;
        Endpoint = new RavenDBCloudEndpoint(server);
        Subdomain = options.Subdomain ?? DeriveSubdomain(productName);

        // Managed: the product belongs to this AppHost, so its applications' names are enough. Attached: other
        // AppHosts use the product too, so the client name keeps their certificates apart. Aspire resource names have
        // no dots, which tells the two schemes apart.
        CertificateNamePrefix = mode == RavenDBCloudMode.Managed ? "aspire." : $"aspire.{clientName!.ToLowerInvariant()}.";
    }

    /// <summary>The server the deployment publishes.</summary>
    public RavenDBServerResource Server { get; }

    /// <summary>The parameter that holds the Cloud API key.</summary>
    public ParameterResource ApiKey { get; }

    /// <summary>Whether the AppHost manages the product or attaches to it.</summary>
    public RavenDBCloudMode Mode { get; }

    /// <summary>The display name the product is found and created by; its identity.</summary>
    public string ProductName { get; }

    /// <summary>What a managed product is made of; <see langword="null"/> when attached.</summary>
    public RavenDBCloudSpec? Spec { get; }

    /// <summary>The name that keeps this AppHost's certificates apart on an attached product.</summary>
    public string? ClientName { get; }

    /// <summary>The options set in the AppHost.</summary>
    public RavenDBCloudOptions Options { get; }

    /// <summary>The subdomain a new product is created with.</summary>
    public string Subdomain { get; }

    /// <summary>The URLs of the product's nodes, separated by commas, that the applications get.</summary>
    public RavenDBCloudEndpoint Endpoint { get; }

    /// <summary>The certificate values of the applications that use the server, set when the model is complete.</summary>
    public List<RavenDBCloudCertificate> Certificates { get; } = [];

    /// <summary>How this AppHost's certificate names start: <c>aspire.</c>, or <c>aspire.&lt;client name&gt;.</c> when attached.</summary>
    public string CertificateNamePrefix { get; }

    /// <summary>The name the product knows an application's certificate by.</summary>
    public string CertificateName(IResource consumer) => (CertificateNamePrefix + consumer.Name).ToLowerInvariant();

    /// <summary>
    /// Whether a certificate on the product is one this AppHost issues: managed, <c>aspire.&lt;application&gt;</c>;
    /// attached, <c>aspire.&lt;client&gt;.&lt;anything&gt;</c>. Never another client's.
    /// </summary>
    public bool IsOwnCertificate(string? name)
    {
        if (name is null || !name.StartsWith(CertificateNamePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Mode == RavenDBCloudMode.Attached || !name.AsSpan(CertificateNamePrefix.Length).Contains('.');
    }

    /// <summary>Delay between two status checks while a product changes; shortened in tests.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>How long a deployment waits for a new or changed product to become active; creating one takes minutes.</summary>
    public TimeSpan ProvisioningTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>How long a product that still reports Active right after a change request is given to start it.</summary>
    public TimeSpan ChangeGracePeriod { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How long the deployment waits for the product's server to accept connections. Right after a change the product
    /// reports Active a little before its node answers; much longer means this machine's address is not allowed.
    /// </summary>
    public TimeSpan ReachableTimeout { get; set; } = TimeSpan.FromMinutes(5);

    // Found while the deployment runs.

    /// <summary>The product's id, once the deployment found or created it.</summary>
    public string? ProductId { get; set; }

    /// <summary>The URLs of the product's nodes.</summary>
    public IReadOnlyList<string> NodeUrls { get; set; } = [];

    /// <summary>Nodes in the product's cluster; the replication factor of new databases.</summary>
    public int NodeCount { get; set; } = 1;

    /// <summary>
    /// The client admin certificate of the product, downloaded once per deployment and never disposed: the RavenDB client
    /// caches its HTTP clients per certificate thumbprint and keeps the first instance it was given, so a disposed one
    /// breaks every later TLS handshake with the product.
    /// </summary>
    public X509Certificate2? AdminCertificate { get; set; }

    /// <summary>The plan the deployment follows; computed by the plan step.</summary>
    public RavenDBCloudPlan? Plan { get; set; }

    /// <summary>
    /// The product name's letters, digits and dashes; when that is too long, its beginning and a hash of the whole name,
    /// so <c>shop-staging</c> and <c>shop-production</c> still get different subdomains.
    /// </summary>
    internal static string DeriveSubdomain(string productName)
    {
        var allowed = new string([.. productName.ToLowerInvariant().Where(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')]).Trim('-');

        if (allowed.Length is > 0 and <= MaxSubdomainLength)
        {
            return allowed;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(productName)))[..5].ToLowerInvariant();
        var prefix = allowed[..Math.Min(allowed.Length, MaxSubdomainLength - hash.Length - 1)].TrimEnd('-');

        return prefix.Length == 0 ? $"p{hash}" : $"{prefix}-{hash}";
    }

    /// <summary>Whether RavenDB Cloud accepts <paramref name="subdomain"/>.</summary>
    internal static bool IsValidSubdomain(string subdomain) =>
        subdomain.Length is > 0 and <= MaxSubdomainLength &&
        subdomain.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') &&
        !subdomain.StartsWith('-') &&
        !subdomain.EndsWith('-');
}
