using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.RavenDB.Cloud;

/// <summary>One line of the plan, as the deployment prints it.</summary>
/// <param name="Text">What happens, such as <c>change: instance Dev10 -> Dev20</c>.</param>
/// <param name="Blocks">Whether the line stops the deployment.</param>
internal sealed record RavenDBCloudPlanLine(string Text, bool Blocks = false);

/// <summary>
/// What the deployment will do to the product, decided before anything is built or changed: every difference between
/// the AppHost and the product either stops the deployment or is applied with explicit consent.
/// </summary>
internal sealed class RavenDBCloudPlan
{
    /// <summary>The lines the person who deploys reads, in order.</summary>
    public List<RavenDBCloudPlanLine> Lines { get; } = [];

    /// <summary>The product found by name; <see langword="null"/> when there is none.</summary>
    public string? ProductId { get; set; }

    /// <summary>The details of the product found by name; <see langword="null"/> when there is none.</summary>
    public ProductDetails? Product { get; set; }

    /// <summary>The product to create; <see langword="null"/> when it exists or the AppHost attaches to one.</summary>
    public ProductCreateRequest? Create { get; set; }

    /// <summary>The instance type the product changes to; <see langword="null"/> when it stays.</summary>
    public string? InstanceTypeChange { get; set; }

    /// <summary>The change of the product's data disk; <see langword="null"/> when it stays.</summary>
    public ChangeStorageRequest? StorageChange { get; set; }

    /// <summary>Whether the plan changes the product.</summary>
    public bool HasChanges => InstanceTypeChange is not null || StorageChange is not null;

    /// <summary>Whether a line stops the deployment.</summary>
    public bool Blocks => Lines.Any(l => l.Blocks);

    /// <summary>Adds a line; a blocking one stops the deployment.</summary>
    public void Add(string text, bool blocks = false) => Lines.Add(new RavenDBCloudPlanLine(text, blocks));

    /// <summary>The plan as the deployment log shows it, with <c>!</c> before the lines that stop it.</summary>
    public override string ToString() => string.Join(Environment.NewLine, Lines.Select(l => (l.Blocks ? "  ! " : "  ") + l.Text));
}

/// <summary>Computes the <see cref="RavenDBCloudPlan"/> from the AppHost, the product and the Cloud API's offer.</summary>
internal static class RavenDBCloudPlanner
{
    /// <summary>The compute environments that deliver the product URL and the certificates to the applications.</summary>
    internal static readonly IReadOnlyDictionary<string, string> SupportedEnvironments = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Aspire.Hosting.Docker.DockerComposeEnvironmentResource"] = "Docker Compose",
    };

    /// <param name="client">The Cloud API.</param>
    /// <param name="deployment">The AppHost's side.</param>
    /// <param name="consumers">The applications that use the server.</param>
    /// <param name="approved">Whether the deployment approves changes to the product.</param>
    /// <param name="cancellationToken">Cancels the calls.</param>
    public static async Task<RavenDBCloudPlan> PlanAsync(
        RavenDBCloudApiClient client,
        RavenDBCloudDeployment deployment,
        IReadOnlyList<RavenDBConsumer> consumers,
        bool approved,
        CancellationToken cancellationToken)
    {
        var plan = new RavenDBCloudPlan();
        var (productId, product) = await RavenDBCloudProducts.FindAsync(client, deployment.ProductName, cancellationToken).ConfigureAwait(false);
        plan.ProductId = productId;
        plan.Product = product;

        foreach (var range in deployment.Options.AllowedIps)
        {
            if (!IPNetwork.TryParse(range.Trim(), out var network))
            {
                plan.Add(
                    $"allowed IP range '{range}' is not a valid CIDR range: write it as RavenDB Cloud takes it, such as " +
                    "203.0.113.0/24, or 203.0.113.7/32 for one address.",
                    blocks: true);
            }
            else if (network.PrefixLength < (network.BaseAddress.AddressFamily == AddressFamily.InterNetwork ? 16 : 48))
            {
                plan.Add(
                    $"wide allowed IPs: {range} lets a large part of the internet reach '{deployment.ProductName}', which then " +
                    "relies on its certificates alone. Allow the outbound addresses of the machines that connect instead.");
            }
        }

        // RavenDB Cloud could not set the product up, or waits for a payment: it can neither change nor serve.
        if (product?.Status is ProductStatus.Error)
        {
            plan.Add($"error: '{deployment.ProductName}' is in error in RavenDB Cloud. See the portal for details.", blocks: true);
        }
        else if (product?.Status is ProductStatus.AwaitingPayment)
        {
            plan.Add($"awaiting payment: '{deployment.ProductName}' waits for a payment. Complete it in the portal and deploy again.", blocks: true);
        }
        else if (deployment.Mode == RavenDBCloudMode.Attached)
        {
            if (product is null)
            {
                plan.Add(
                    $"missing: there is no RavenDB Cloud product '{deployment.ProductName}'. PublishAsExistingRavenDBCloud never creates " +
                    "one: create it in the portal, or use PublishAsRavenDBCloud.",
                    blocks: true);
            }
            else
            {
                plan.Add($"attach: '{deployment.ProductName}' ({Describe(product)}), as client '{deployment.ClientName}'");
            }
        }
        else
        {
            await PlanManagedAsync(client, deployment, plan, approved, cancellationToken).ConfigureAwait(false);
        }

        if (product is not null)
        {
            CheckAllowedIps(deployment, product, plan);
        }

        CheckEnvironments(deployment, consumers, plan);

        return plan;
    }

    private static async Task PlanManagedAsync(
        RavenDBCloudApiClient client,
        RavenDBCloudDeployment deployment,
        RavenDBCloudPlan plan,
        bool approved,
        CancellationToken cancellationToken)
    {
        var spec = deployment.Spec!;
        var offered = await CheckOfferAsync(client, deployment, spec, plan, cancellationToken).ConfigureAwait(false);

        if (plan.Product is not { } product)
        {
            switch (await RavenDBCloudProducts.FindNameHolderAsync(client, deployment.ProductName, cancellationToken).ConfigureAwait(false))
            {
                case null:
                    break;

                // A destroy that terminated it moments ago: it is hidden, and the name freed, once it is terminated.
                case ProductStatus.Terminating:
                    plan.Add(
                        $"name: '{deployment.ProductName}' is held by a product that is still being terminated. Deploy again once " +
                        "RavenDB Cloud has terminated it.",
                        blocks: true);
                    return;

                default:
                    plan.Add(
                        $"name: '{deployment.ProductName}' is held by a terminated product, which RavenDB Cloud keeps until it is " +
                        "hidden. Hide that product in the RavenDB Cloud portal, or choose another name.",
                        blocks: true);
                    return;
            }

            PlanCreate(deployment, spec, offered, plan);
            return;
        }

        var changes = new List<string>();
        var impossible = new List<string>();
        void CannotChange(string change) => impossible.Add($"{change}. Create a product with another name, or change the AppHost back.");

        if (!Same(product.CloudProvider, spec.Provider.ToString()))
        {
            CannotChange($"provider {product.CloudProvider} -> {spec.Provider}");
        }

        if (!Same(product.Region, spec.Region))
        {
            CannotChange($"region {product.Region} -> {spec.Region}");
        }

        // There is no Cloud API call to change the release channel.
        if (!Same(product.ReleaseChannel, spec.ReleaseChannel))
        {
            CannotChange($"release channel {product.ReleaseChannel} -> {spec.ReleaseChannel} (change it in the portal)");
        }

        // The Cloud API has no call that moves a product's data to another disk layout.
        if (product.HardwareInfo is { } hardware && LayoutOf(hardware) != spec.DiskLayout)
        {
            CannotChange($"disk layout {LayoutOf(hardware)} -> {spec.DiskLayout}");
        }

        // The Cloud API refuses an instance type of another tier ("not available for the product tier").
        if (!Same(product.Tier, spec.Tier.ToString()))
        {
            CannotChange($"tier {product.Tier} -> {spec.Tier} (RavenDB Cloud cannot change the tier of a product)");
        }
        else if (!Same(product.InstanceType, spec.InstanceType))
        {
            changes.Add($"instance {product.InstanceType} -> {spec.InstanceType}");
            plan.InstanceTypeChange = spec.InstanceType;
        }

        if (product.HardwareInfo?.DataDisk is { } storage)
        {
            var size = spec.DiskSizeInGb;
            var storageChanges = new List<string>();

            // Usually nobody changed the AppHost: RavenDB Cloud grows a disk that fills up.
            if (size < storage.Size)
            {
                impossible.Add(
                    $"disk {storage.Size} GB -> {size} GB. A disk cannot shrink, and RavenDB Cloud grows one that fills up: " +
                    $"set the disk size in the AppHost to {storage.Size} GB.");
            }
            else if (size > storage.Size)
            {
                storageChanges.Add($"disk {storage.Size} GB -> {size} GB");
            }

            if (!Same(storage.Type, spec.StorageType.ToString()))
            {
                storageChanges.Add($"storage {storage.Type} -> {spec.StorageType}");
            }

            if (spec.Iops is { } iops && iops != storage.Iops)
            {
                storageChanges.Add($"IOPS {Show(storage.Iops)} -> {iops}");
            }

            if (spec.Throughput is { } throughput && throughput != storage.Throughput)
            {
                storageChanges.Add($"throughput {Show(storage.Throughput)} -> {throughput} MB/s");
            }

            if (storageChanges.Count > 0 && size >= storage.Size)
            {
                changes.AddRange(storageChanges);
                plan.StorageChange = new ChangeStorageRequest(
                    plan.ProductId!,
                    spec.StorageType.ToString(),
                    size,
                    spec.Iops ?? storage.Iops,
                    spec.Throughput ?? storage.Throughput);
            }
        }

        foreach (var line in impossible)
        {
            plan.Add($"impossible: {line}", blocks: true);
        }

        if (changes.Count == 0)
        {
            if (impossible.Count == 0)
            {
                plan.Add($"no changes: '{deployment.ProductName}' ({Describe(product)})");
            }

            return;
        }

        var change = $"change '{deployment.ProductName}': {string.Join(", ", changes)}";

        if (approved)
        {
            plan.Add($"{change} (approved by {RavenDBCloudDeploymentApproval.Variable})");
        }
        else
        {
            plan.InstanceTypeChange = null;
            plan.StorageChange = null;
            plan.Add(
                $"{change}. Deploy with {RavenDBCloudDeploymentApproval.Variable}=true to apply it.",
                blocks: true);
        }
    }

    private static void PlanCreate(RavenDBCloudDeployment deployment, RavenDBCloudSpec spec, bool offered, RavenDBCloudPlan plan)
    {
        if (deployment.Options.AllowedIps.Count == 0)
        {
            plan.Add(
                $"creating '{deployment.ProductName}' needs at least one allowed IP range, and the Cloud API cannot add them later. " +
                "Set them with options.WithAllowedIps(...): the machine that deploys, the CI runner, and the outbound addresses " +
                "of the applications' environment.",
                blocks: true);
        }


        if (!RavenDBCloudDeployment.IsValidSubdomain(deployment.Subdomain))
        {
            plan.Add(
                $"subdomain '{deployment.Subdomain}' is not valid: use up to {RavenDBCloudDeployment.MaxSubdomainLength} letters, " +
                "digits and dashes, not starting or ending with a dash.",
                blocks: true);
        }

        if (!offered)
        {
            return;
        }

        plan.Create = new ProductCreateRequest(
            CloudProvider: spec.Provider.ToString(),
            InstanceTypeName: spec.InstanceType,
            DisplayName: deployment.ProductName,
            ReleaseChannel: spec.ReleaseChannel,
            SubdomainName: deployment.Subdomain,
            Tier: spec.Tier.ToString(),
            Region: spec.Region,
            DiskSize: spec.DiskSizeInGb,
            Iops: spec.Iops,
            Throughput: spec.Throughput,
            StorageTypeName: spec.StorageType.ToString(),
            AllowedIps: [.. deployment.Options.AllowedIps],
            DiskLayout: spec.DiskLayout.ToString());

        if (!plan.Blocks)
        {
            plan.Add(
                $"create '{deployment.ProductName}': {spec}, allowed IPs {string.Join(", ", deployment.Options.AllowedIps)}");
        }
    }

    /// <summary>Whether RavenDB Cloud offers <paramref name="spec"/>, with a blocking line for anything it does not.</summary>
    private static async Task<bool> CheckOfferAsync(
        RavenDBCloudApiClient client,
        RavenDBCloudDeployment deployment,
        RavenDBCloudSpec spec,
        RavenDBCloudPlan plan,
        CancellationToken cancellationToken)
    {
        // The channel does not depend on the region or the instance type, so it is checked even when they are wrong.
        var offered = true;
        var channels = (await client.GetReleaseChannelsAsync(cancellationToken).ConfigureAwait(false)).ReleaseChannels?
            .Where(c => c.Name is not null)
            .ToList() ?? [];

        if (channels.Count > 0 && !channels.Any(c => Same(c.Name, spec.ReleaseChannel)))
        {
            var available = channels.Select(c => c.RavenVersion is { } version ? $"{c.Name} (RavenDB {version})" : c.Name);
            plan.Add($"release channel '{spec.ReleaseChannel}' does not exist. Available: {List(available)}.", blocks: true);
            offered = false;
        }

        var provider = spec.Provider.ToString();
        var regions = await client.GetRegionsAsync(provider, cancellationToken).ConfigureAwait(false);

        if (!regions.Any(r => Same(r, spec.Region)))
        {
            plan.Add($"region '{spec.Region}' is not offered on {provider}. Offered: {List(regions)}.", blocks: true);
            return false;
        }

        var types = await client.GetInstanceTypesAsync(provider, spec.Region, cancellationToken).ConfigureAwait(false);
        var type = types.FirstOrDefault(t => Same(t.Name, spec.InstanceType));
        var ofTier = types.Where(t => t.Name is not null && Same(t.Tier, spec.Tier.ToString())).Select(DescribeInstance).ToList();

        if (type is null || !Same(type.Tier, spec.Tier.ToString()))
        {
            plan.Add(
                $"instance type '{spec.InstanceType}' is not offered for the {spec.Tier} tier in {provider} {spec.Region}. " +
                $"Offered: {List(ofTier)}.",
                blocks: true);
            return false;
        }

        var sizes = type.Parameters?.AvailableDiskSizes?.Where(s => s > 0).ToList() ?? [];

        if (sizes.Count > 0 && !sizes.Contains(spec.DiskSizeInGb))
        {
            plan.Add($"disk size {spec.DiskSizeInGb} GB is not offered for instance type '{type.Name}'. Offered: {List(sizes)} GB.", blocks: true);
            offered = false;
        }

        if (type.Parameters?.AvailableStorageTypes is { Count: > 0 } storageTypes)
        {
            var storage = storageTypes.FirstOrDefault(s => Same(s.Name, spec.StorageType.ToString()));

            if (storage is null)
            {
                plan.Add(
                    $"storage type {spec.StorageType} is not offered for instance type '{type.Name}'. Offered: {List(storageTypes.Select(s => s.Name))}.",
                    blocks: true);
                offered = false;
            }
            else if (spec.Iops is { } iops && storage.IopsRange is { } range && (iops < range.Min || iops > range.Max))
            {
                plan.Add($"{iops} IOPS is outside the range offered for {spec.StorageType}: {range.Min} to {range.Max}.", blocks: true);
                offered = false;
            }
        }

        deployment.NodeCount = Math.Max(type.Parameters?.NumberOfNodes ?? 1, 1);

        return offered;
    }

    /// <summary>Each range the AppHost allows must be covered by one the product allows; the API cannot add it.</summary>
    private static void CheckAllowedIps(RavenDBCloudDeployment deployment, ProductDetails product, RavenDBCloudPlan plan)
    {
        var allowed = (product.Security?.AllowedIps ?? [])
            .Select(r => IPNetwork.TryParse(r, out var parsed) ? parsed : (IPNetwork?)null)
            .OfType<IPNetwork>()
            .ToList();

        // Covered: the requested range lies inside an allowed one.
        var missing = deployment.Options.AllowedIps
            .Where(r => IPNetwork.TryParse(r.Trim(), out var requested) &&
                !allowed.Any(a => requested.PrefixLength >= a.PrefixLength && a.Contains(requested.BaseAddress)))
            .ToList();

        if (missing.Count > 0)
        {
            plan.Add(
                $"allowed IPs: {string.Join(", ", missing)} not allowed on '{deployment.ProductName}'. Add them in the RavenDB Cloud " +
                "portal (the Cloud API cannot change them) and deploy again.",
                blocks: true);
        }
    }

    /// <summary>An application deployed where the URL and certificate cannot be delivered would start without them.</summary>
    private static void CheckEnvironments(RavenDBCloudDeployment deployment, IReadOnlyList<RavenDBConsumer> consumers, RavenDBCloudPlan plan)
    {
        var supported = string.Join(", ", SupportedEnvironments.Values);

        foreach (var consumer in consumers)
        {
            // Exactly one compute environment, of a supported type: anything else could carry the certificate somewhere
            // it is not a secret, or nowhere.
            var environments = consumer.Resource.Annotations.OfType<DeploymentTargetAnnotation>().Select(t => t.ComputeEnvironment).ToList();

            if (environments is [{ } only] && SupportedEnvironments.ContainsKey(only.GetType().FullName ?? string.Empty))
            {
                continue;
            }

            var where = environments switch
            {
                [] or [null] => "no compute environment",
                [{ } one] => $"'{one.Name}'",
                _ => $"{environments.Count} compute environments",
            };

            plan.Add(
                $"no target: '{consumer.Resource.Name}' is deployed to {where}, where the RavenDB Cloud integration cannot deliver " +
                $"the URL and certificate of '{deployment.Server.Name}'. Supported: {supported}, as an application's only compute environment.",
                blocks: true);
        }
    }

    /// <summary>One line about an existing product, in the words of <see cref="RavenDBCloudSpec.ToString"/>.</summary>
    internal static string Describe(ProductDetails product)
    {
        var storage = product.HardwareInfo is { DataDisk: { } s } h ? $", {s.Size} GB {s.Type} on {LayoutOf(h)}" : string.Empty;
        return $"{product.Tier} {product.InstanceType}, {product.CloudProvider} {product.Region}{storage}, channel {product.ReleaseChannel}";
    }

    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>A product with a data disk keeps its data there; one without keeps it on the system disk.</summary>
    private static RavenDBCloudDiskLayout LayoutOf(HardwareInfo hardware) =>
        hardware.AdditionalStorage is null ? RavenDBCloudDiskLayout.RootOnly : RavenDBCloudDiskLayout.SingleDataDisk;

    /// <summary>An instance type with what it is, such as <c>Dev10 (2 vCPU, 0.5 GB RAM, 1 node, disks 10/15/20/25 GB)</c>.</summary>
    private static string DescribeInstance(InstanceTypeItem type)
    {
        var parameters = type.Parameters;
        var details = new List<string>();

        if (parameters?.VirtualCpus is { } cpus)
        {
            details.Add($"{cpus} vCPU");
        }

        if (parameters?.Ram is { } ram)
        {
            details.Add($"{ram.ToString("0.#", CultureInfo.InvariantCulture)} GB RAM");
        }

        if (parameters?.NumberOfNodes is { } nodes)
        {
            details.Add(nodes == 1 ? "1 node" : $"{nodes} nodes");
        }

        if (parameters?.AvailableDiskSizes is { Count: > 0 } disks)
        {
            details.Add($"disks {string.Join("/", disks)} GB");
        }

        return details.Count == 0 ? type.Name! : $"{type.Name} ({string.Join(", ", details)})";
    }

    private static string Show(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "default";

    private static string Show(double? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "default";

    private static string List<T>(IEnumerable<T?> values)
    {
        var items = values.Where(v => v is not null).Select(v => Convert.ToString(v, CultureInfo.InvariantCulture)).ToList();
        return items.Count == 0 ? "none" : string.Join(", ", items);
    }

}

/// <summary>The environment variable that approves changes to products.</summary>
internal static class RavenDBCloudDeploymentApproval
{
    /// <summary>
    /// The environment variable that approves the changes the plan shows. It is read from the process environment only,
    /// so a committed settings file cannot approve every change for good.
    /// </summary>
    public const string Variable = "RavenDB__Cloud__ApproveChanges";
}
