#pragma warning disable ASPIREATS001 // AspireExport is experimental

using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.RavenDB.Cloud;

/// <summary>
/// What a RavenDB Cloud product is made of: everything that decides its cost and where its data lives. Nothing here
/// has a default: a deployment creates exactly this, and stops when an existing product differs from it.
/// </summary>
/// <remarks>
/// Create one with <see cref="Development"/> or <see cref="Production"/>. On every deployment the plan checks the spec
/// against what RavenDB Cloud offers the account in the region and, when a value is not offered, lists the valid ones:
/// the regions, the instance types with their size, and the release channels with their RavenDB version. RavenDB Cloud
/// does not create free products through its API: attach to one created in the portal with
/// <c>PublishAsExistingRavenDBCloud</c>.
/// </remarks>
[AspireExport(ExposeProperties = true)]
public sealed class RavenDBCloudSpec
{
    private RavenDBCloudSpec(
        RavenDBCloudTier tier,
        RavenDBCloudProvider provider,
        string region,
        string instanceType,
        int diskSizeInGb,
        RavenDBCloudStorageType storageType,
        RavenDBCloudDiskLayout diskLayout,
        string releaseChannel,
        int? iops,
        int? throughput)
    {
        Tier = tier;
        Provider = provider;
        Region = region;
        InstanceType = instanceType;
        DiskSizeInGb = diskSizeInGb;
        StorageType = storageType;
        DiskLayout = diskLayout;
        ReleaseChannel = releaseChannel;
        Iops = iops;
        Throughput = throughput;
    }

    /// <summary>The product tier.</summary>
    public RavenDBCloudTier Tier { get; }

    /// <summary>The cloud provider.</summary>
    public RavenDBCloudProvider Provider { get; }

    /// <summary>The provider region, such as <c>eu-central-1</c> on AWS or <c>westeurope</c> on Azure.</summary>
    public string Region { get; }

    /// <summary>The instance type, such as <c>Dev10</c> or <c>P20</c>.</summary>
    public string InstanceType { get; }

    /// <summary>The data disk size in GB.</summary>
    public int DiskSizeInGb { get; }

    /// <summary>The data disk type.</summary>
    public RavenDBCloudStorageType StorageType { get; }

    /// <summary>Where the data lives: on the system disk or on a data disk of its own.</summary>
    public RavenDBCloudDiskLayout DiskLayout { get; }

    /// <summary>The release channel, which decides the RavenDB version line, such as <c>Stable72</c>.</summary>
    public string ReleaseChannel { get; }

    /// <summary>Provisioned IOPS of a premium disk; <see langword="null"/> uses the provider's default.</summary>
    public int? Iops { get; }

    /// <summary>Provisioned throughput of a premium disk, in MB/s; <see langword="null"/> uses the provider's default.</summary>
    public int? Throughput { get; }

    /// <summary>A development tier product: one node, no automatic backups.</summary>
    /// <param name="provider">The cloud provider.</param>
    /// <param name="region">The provider's region code, such as <c>eu-central-1</c> on AWS or <c>westeurope</c> on Azure.</param>
    /// <param name="instanceType">The instance type, such as <c>Dev10</c>.</param>
    /// <param name="diskSizeInGb">The data disk size in GB; each instance type offers its own sizes.</param>
    /// <param name="storageType">The data disk type.</param>
    /// <param name="diskLayout">Where the data lives; RavenDB Cloud decides which layouts the tier and instance type allow.</param>
    /// <param name="releaseChannel">The release channel, which decides the RavenDB version line, such as <c>Stable72</c>.</param>
    /// <param name="iops">Provisioned IOPS of a premium disk; omit for the provider's default.</param>
    /// <param name="throughput">Provisioned throughput of a premium disk in MB/s; omit for the provider's default.</param>
    /// <returns>The specification.</returns>
    [AspireExportIgnore(Reason = "Polyglot AppHosts choose the spec with RavenDBCloudSpecBuilder.")]
    public static RavenDBCloudSpec Development(
        RavenDBCloudProvider provider,
        string region,
        string instanceType,
        int diskSizeInGb,
        RavenDBCloudStorageType storageType,
        RavenDBCloudDiskLayout diskLayout,
        string releaseChannel,
        int? iops = null,
        int? throughput = null) =>
        Paid(RavenDBCloudTier.Development, provider, region, instanceType, diskSizeInGb, storageType, diskLayout, releaseChannel, iops, throughput);

    /// <summary>A production tier product: three nodes and automatic backups.</summary>
    /// <param name="provider">The cloud provider.</param>
    /// <param name="region">The provider's region code, such as <c>eu-central-1</c> on AWS or <c>westeurope</c> on Azure.</param>
    /// <param name="instanceType">The instance type, such as <c>P20</c>.</param>
    /// <param name="diskSizeInGb">The data disk size in GB; each instance type offers its own sizes.</param>
    /// <param name="storageType">The data disk type.</param>
    /// <param name="diskLayout">Where the data lives; RavenDB Cloud decides which layouts the tier and instance type allow.</param>
    /// <param name="releaseChannel">The release channel, which decides the RavenDB version line, such as <c>Stable72</c>.</param>
    /// <param name="iops">Provisioned IOPS of a premium disk; omit for the provider's default.</param>
    /// <param name="throughput">Provisioned throughput of a premium disk in MB/s; omit for the provider's default.</param>
    /// <returns>The specification.</returns>
    [AspireExportIgnore(Reason = "Polyglot AppHosts choose the spec with RavenDBCloudSpecBuilder.")]
    public static RavenDBCloudSpec Production(
        RavenDBCloudProvider provider,
        string region,
        string instanceType,
        int diskSizeInGb,
        RavenDBCloudStorageType storageType,
        RavenDBCloudDiskLayout diskLayout,
        string releaseChannel,
        int? iops = null,
        int? throughput = null) =>
        Paid(RavenDBCloudTier.Production, provider, region, instanceType, diskSizeInGb, storageType, diskLayout, releaseChannel, iops, throughput);

    private static RavenDBCloudSpec Paid(
        RavenDBCloudTier tier,
        RavenDBCloudProvider provider,
        string region,
        string instanceType,
        int diskSizeInGb,
        RavenDBCloudStorageType storageType,
        RavenDBCloudDiskLayout diskLayout,
        string releaseChannel,
        int? iops,
        int? throughput)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(releaseChannel);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(diskSizeInGb);

        if (iops is { } i)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(i, nameof(iops));
        }

        if (throughput is { } t)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(t, nameof(throughput));
        }

        return new(tier, provider, region.Trim(), instanceType.Trim(), diskSizeInGb, storageType, diskLayout, releaseChannel.Trim(), iops, throughput);
    }

    /// <summary>One line for logs and the plan, such as <c>Development Dev10, Aws eu-central-1, 20 GB SsdStandard on SingleDataDisk, channel Stable72</c>.</summary>
    public override string ToString()
    {
        var performance = (Iops, Throughput) switch
        {
            (null, null) => string.Empty,
            ({ } i, null) => $" ({i} IOPS)",
            (null, { } t) => $" ({t} MB/s)",
            ({ } i, { } t) => $" ({i} IOPS, {t} MB/s)",
        };
        return $"{Tier} {InstanceType}, {Provider} {Region}, {DiskSizeInGb} GB {StorageType}{performance} on {DiskLayout}, channel {ReleaseChannel}";
    }
}

/// <summary>
/// Chooses the <see cref="RavenDBCloudSpec"/> of a product in a polyglot AppHost, which cannot call the static
/// factories: call exactly one of its methods.
/// </summary>
[AspireExport(ExposeMethods = true)]
public sealed class RavenDBCloudSpecBuilder
{
    /// <summary>The spec the callback chose; <see langword="null"/> until it chooses one.</summary>
    internal RavenDBCloudSpec? Spec { get; private set; }

    /// <summary>Chooses <see cref="RavenDBCloudSpec.Development"/>: one node, no automatic backups.</summary>
    /// <param name="provider">The cloud provider.</param>
    /// <param name="region">The provider's region code, such as <c>eu-central-1</c> on AWS or <c>westeurope</c> on Azure.</param>
    /// <param name="instanceType">The instance type, such as <c>Dev10</c>.</param>
    /// <param name="diskSizeInGb">The data disk size in GB; each instance type offers its own sizes.</param>
    /// <param name="storageType">The data disk type.</param>
    /// <param name="diskLayout">Where the data lives; RavenDB Cloud decides which layouts the tier and instance type allow.</param>
    /// <param name="releaseChannel">The release channel, which decides the RavenDB version line, such as <c>Stable72</c>.</param>
    /// <param name="iops">Provisioned IOPS of a premium disk; omit for the provider's default.</param>
    /// <param name="throughput">Provisioned throughput of a premium disk in MB/s; omit for the provider's default.</param>
    public void Development(RavenDBCloudProvider provider, string region, string instanceType, int diskSizeInGb, RavenDBCloudStorageType storageType, RavenDBCloudDiskLayout diskLayout, string releaseChannel, int? iops = null, int? throughput = null) =>
        Choose(RavenDBCloudSpec.Development(provider, region, instanceType, diskSizeInGb, storageType, diskLayout, releaseChannel, iops, throughput));

    /// <summary>Chooses <see cref="RavenDBCloudSpec.Production"/>: three nodes and automatic backups.</summary>
    /// <param name="provider">The cloud provider.</param>
    /// <param name="region">The provider's region code, such as <c>eu-central-1</c> on AWS or <c>westeurope</c> on Azure.</param>
    /// <param name="instanceType">The instance type, such as <c>P20</c>.</param>
    /// <param name="diskSizeInGb">The data disk size in GB; each instance type offers its own sizes.</param>
    /// <param name="storageType">The data disk type.</param>
    /// <param name="diskLayout">Where the data lives; RavenDB Cloud decides which layouts the tier and instance type allow.</param>
    /// <param name="releaseChannel">The release channel, which decides the RavenDB version line, such as <c>Stable72</c>.</param>
    /// <param name="iops">Provisioned IOPS of a premium disk; omit for the provider's default.</param>
    /// <param name="throughput">Provisioned throughput of a premium disk in MB/s; omit for the provider's default.</param>
    public void Production(RavenDBCloudProvider provider, string region, string instanceType, int diskSizeInGb, RavenDBCloudStorageType storageType, RavenDBCloudDiskLayout diskLayout, string releaseChannel, int? iops = null, int? throughput = null) =>
        Choose(RavenDBCloudSpec.Production(provider, region, instanceType, diskSizeInGb, storageType, diskLayout, releaseChannel, iops, throughput));

    private void Choose(RavenDBCloudSpec spec)
    {
        if (Spec is not null)
        {
            throw new InvalidOperationException($"The RavenDB Cloud product is already specified as {Spec}; call one method of RavenDBCloudSpecBuilder only.");
        }

        Spec = spec;
    }
}
