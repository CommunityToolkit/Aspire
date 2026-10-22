#pragma warning disable ASPIREATS001 // AspireExport is experimental

using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.RavenDB.Cloud;

/// <summary>Cloud provider a RavenDB Cloud product runs on.</summary>
public enum RavenDBCloudProvider
{
    /// <summary>Amazon Web Services.</summary>
    Aws,

    /// <summary>Microsoft Azure.</summary>
    Azure,

    /// <summary>Google Cloud Platform.</summary>
    Gcp,
}

/// <summary>
/// RavenDB Cloud product tier. The free tier is not here: RavenDB Cloud does not create free products through its API,
/// so attach to one created in the portal with <c>PublishAsExistingRavenDBCloud</c>.
/// </summary>
public enum RavenDBCloudTier
{
    /// <summary>Development tier.</summary>
    Development,

    /// <summary>Production tier.</summary>
    Production,
}

/// <summary>Storage type of a product's data disk.</summary>
public enum RavenDBCloudStorageType
{
    /// <summary>Standard SSD.</summary>
    SsdStandard,

    /// <summary>Premium SSD, with provisioned IOPS and throughput where the provider offers them.</summary>
    SsdPremium,
}

/// <summary>
/// Where a product keeps its data. RavenDB Cloud decides which layouts a tier and instance type allow, and a product
/// keeps its layout for life: the Cloud API cannot change it.
/// </summary>
public enum RavenDBCloudDiskLayout
{
    /// <summary>On the instance's system disk.</summary>
    RootOnly,

    /// <summary>On a data disk of its own, apart from the system disk.</summary>
    SingleDataDisk,
}

/// <summary>
/// Optional settings of a server published to RavenDB Cloud. Everything that decides cost or where the data lives is
/// in <see cref="RavenDBCloudSpec"/> instead, which is required.
/// </summary>
[AspireExport(ExposeProperties = true, ExposeMethods = true)]
public sealed class RavenDBCloudOptions
{
    /// <summary>
    /// IP ranges, in CIDR notation, that reach the product: the machine that runs <c>aspire deploy</c> (or the CI
    /// runner), and the outbound addresses of the environment the applications run in. RavenDB Cloud rejects
    /// everything else.
    /// </summary>
    /// <remarks>
    /// A product the deployment creates gets these ranges; the Cloud API requires at least one and offers no way to
    /// change them later, so from then on they are edited in the portal. On every deployment the plan checks that each
    /// range is allowed on the product and stops, naming the missing ones, when one is not.
    /// </remarks>
    public IList<string> AllowedIps { get; } = [];

    /// <summary>
    /// Subdomain of the node URLs of a product the deployment creates: up to 13 letters, digits and dashes, unique in
    /// the account. When unset, it is derived from the product name.
    /// </summary>
    public string? Subdomain { get; set; }

    /// <summary>
    /// Terminates the product, found by its name, in <c>aspire destroy</c>. Off by default: terminating a product
    /// deletes its data. Only for <c>PublishAsRavenDBCloud</c>: a product the AppHost attaches to is never terminated.
    /// </summary>
    public bool TerminateOnDestroy { get; set; }

    /// <summary>Allows the given IP ranges to reach the product. See <see cref="AllowedIps"/>.</summary>
    /// <param name="allowedIps">IP ranges in CIDR notation, such as <c>203.0.113.0/24</c>.</param>
    /// <returns>These options, for chaining.</returns>
    public RavenDBCloudOptions WithAllowedIps(params string[] allowedIps)
    {
        ArgumentNullException.ThrowIfNull(allowedIps);

        foreach (var range in allowedIps)
        {
            AllowedIps.Add(range);
        }

        return this;
    }
}
