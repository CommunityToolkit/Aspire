using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.RavenDB.Cloud;

/// <summary>
/// The lifecycle of a RavenDB Cloud product through the Cloud API: create it or apply the approved plan, wait until it
/// is active, and terminate it on destroy when asked to. The product is always found by its name, from any machine.
/// </summary>
internal sealed class RavenDBCloudProducts(RavenDBCloudApiClient client, ILogger logger)
{
    /// <summary>
    /// The live product named <paramref name="productName"/>. A terminated product stays listed for a while, so only
    /// live ones count, and two live ones with the same name are an error: the name is the product's identity.
    /// </summary>
    public static async Task<(string? ProductId, ProductDetails? Details)> FindAsync(
        RavenDBCloudApiClient client,
        string productName,
        CancellationToken cancellationToken)
    {
        var live = new List<(string Id, ProductDetails Details)>();

        foreach (var match in (await client.ListProductsAsync(cancellationToken).ConfigureAwait(false))
            .Where(p => p.Id is not null && string.Equals(p.Name, productName, StringComparison.OrdinalIgnoreCase)))
        {
            if (await client.GetProductAsync(match.Id!, cancellationToken).ConfigureAwait(false) is { } details && !IsGone(details.Status))
            {
                live.Add((match.Id!, details));
            }
        }

        return live.Count switch
        {
            0 => (null, null),
            1 => live[0],
            _ => throw new InvalidOperationException(
                $"The account has {live.Count} RavenDB Cloud products named '{productName}'. The name identifies the product: " +
                "rename or terminate the extra ones in the portal."),
        };
    }

    /// <summary>
    /// The status of a product the portal still shows under <paramref name="productName"/>, such as a terminated one:
    /// RavenDB Cloud keeps names unique among those until they are hidden. Asked when no live product has the name.
    /// </summary>
    /// <returns>The holder's status, or <see langword="null"/> when the name is free.</returns>
    public static async Task<string?> FindNameHolderAsync(RavenDBCloudApiClient client, string productName, CancellationToken cancellationToken)
    {
        foreach (var match in (await client.ListProductsAsync(cancellationToken).ConfigureAwait(false))
            .Where(p => p.Id is not null && string.Equals(p.Name, productName, StringComparison.OrdinalIgnoreCase)))
        {
            return (await client.GetProductAsync(match.Id!, cancellationToken).ConfigureAwait(false))?.Status ?? ProductStatus.Terminated;
        }

        return null;
    }

    /// <summary>Carries out <paramref name="plan"/> and resolves the product's URL on <paramref name="deployment"/>.</summary>
    public async Task ProvisionAsync(RavenDBCloudDeployment deployment, RavenDBCloudPlan plan, CancellationToken cancellationToken)
    {
        if (plan.Blocks)
        {
            throw new InvalidOperationException($"The plan for RavenDB Cloud product '{deployment.ProductName}' stops the deployment:{Environment.NewLine}{plan}");
        }

        var productId = plan.ProductId;
        ProductDetails? details = plan.Product;

        if (productId is null)
        {
            var request = plan.Create
                ?? throw new InvalidOperationException($"RavenDB Cloud product '{deployment.ProductName}' does not exist and the plan does not create it.");

            logger.LogInformation("Creating RavenDB Cloud product '{Product}': {Spec}.", deployment.ProductName, deployment.Spec);

            productId = await client.CreateProductAsync(request, cancellationToken).ConfigureAwait(false);
            details = null;
        }
        else
        {
            logger.LogInformation("Using RavenDB Cloud product '{Product}' ({ProductId}).", deployment.ProductName, productId);
        }

        details = await WaitForActiveAsync(deployment, productId, details, cancellationToken).ConfigureAwait(false);

        if (plan.HasChanges)
        {
            details = await ApplyChangesAsync(deployment, plan, productId, cancellationToken).ConfigureAwait(false);
        }

        Resolve(deployment, productId, details);
        logger.LogInformation("RavenDB Cloud product '{Product}' is active at {Url}.", deployment.ProductName, deployment.Endpoint.Value);
    }

    /// <summary>
    /// Terminates a product the AppHost manages when <see cref="RavenDBCloudOptions.TerminateOnDestroy"/> is set; any
    /// other product is left running.
    /// </summary>
    /// <returns>The product left running, resolved on <paramref name="deployment"/> when it has a URL; else <see langword="null"/>.</returns>
    public async Task<ProductDetails?> DestroyAsync(RavenDBCloudDeployment deployment, CancellationToken cancellationToken)
    {
        var (productId, details) = await FindAsync(client, deployment.ProductName, cancellationToken).ConfigureAwait(false);

        if (productId is null || details is null)
        {
            logger.LogInformation("RavenDB Cloud product '{Product}' does not exist; nothing to destroy.", deployment.ProductName);
            return null;
        }

        if (deployment.Mode == RavenDBCloudMode.Managed && deployment.Options.TerminateOnDestroy)
        {
            // The name alone could be another environment's product: terminate only the one the AppHost describes.
            if (Mismatches(deployment, details) is { Count: > 0 } mismatches)
            {
                throw new InvalidOperationException(
                    $"RavenDB Cloud product '{deployment.ProductName}' ({RavenDBCloudPlanner.Describe(details)}) does not match the " +
                    $"AppHost ({string.Join(", ", mismatches)}), so it is not terminated. If it belongs to another environment, give " +
                    "this one a product name of its own; if the AppHost should terminate it, make the AppHost describe it.");
            }

            // Works whatever the product's state, also while it is being created and has no URL yet.
            logger.LogInformation(
                "Terminating RavenDB Cloud product '{Product}' ({ProductId}: {Description}). Its data is deleted.",
                deployment.ProductName,
                productId,
                RavenDBCloudPlanner.Describe(details));
            await client.TerminateProductAsync(productId, cancellationToken).ConfigureAwait(false);
            return null;
        }

        logger.LogInformation(
            deployment.Mode == RavenDBCloudMode.Attached
                ? "RavenDB Cloud product '{Product}' ({ProductId}) is attached to, so it is left running."
                : "RavenDB Cloud product '{Product}' ({ProductId}) is left running because TerminateOnDestroy is not set. Terminate it in the portal once it is no longer needed.",
            deployment.ProductName,
            productId);

        if (details.Dns?.Any(d => !string.IsNullOrWhiteSpace(d)) != true)
        {
            return null;
        }

        Resolve(deployment, productId, details);
        return details;
    }

    /// <summary>
    /// What tells the product found by name apart from the one the AppHost describes: what RavenDB Cloud never changes,
    /// and the instance type. The disk is left out, since RavenDB Cloud grows a disk that fills up, and so is the
    /// subdomain, which a product made in the portal chose freely.
    /// </summary>
    private static List<string> Mismatches(RavenDBCloudDeployment deployment, ProductDetails product)
    {
        var spec = deployment.Spec!;
        var mismatches = new List<string>();

        void Compare(string what, string? actual, string expected)
        {
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                mismatches.Add($"{what} {actual ?? "unknown"}, the AppHost says {expected}");
            }
        }

        Compare("tier", product.Tier, spec.Tier.ToString());
        Compare("provider", product.CloudProvider, spec.Provider.ToString());
        Compare("region", product.Region, spec.Region);
        Compare("release channel", product.ReleaseChannel, spec.ReleaseChannel);
        Compare("instance type", product.InstanceType, spec.InstanceType);

        return mismatches;
    }

    /// <summary>The product's client admin certificate, from the Cloud API; downloaded once per deployment.</summary>
    public async Task<X509Certificate2> GetAdminCertificateAsync(RavenDBCloudDeployment deployment, CancellationToken cancellationToken)
    {
        if (deployment.AdminCertificate is null)
        {
            var productId = deployment.ProductId
                ?? throw new InvalidOperationException($"RavenDB Cloud product '{deployment.ProductName}' has not been resolved yet.");
            var bundle = await client.GetAdminCertificateAsync(productId, cancellationToken).ConfigureAwait(false);
            var pfx = RavenDBCloudCertificates.ExtractPfx(bundle)
                ?? throw new InvalidOperationException($"The certificate bundle of RavenDB Cloud product '{deployment.ProductName}' contains no .pfx file.");

            var certificate = RavenDBCloudCertificates.Load(pfx);
            deployment.AdminCertificate = certificate;

            // Every connection of the deployment uses this one instance, so it lives as long as the process. On Windows
            // its private key is a file in the user profile until the certificate is disposed: dispose it on exit.
            AppDomain.CurrentDomain.ProcessExit += (_, _) => certificate.Dispose();
        }

        return deployment.AdminCertificate;
    }

    private async Task<ProductDetails> ApplyChangesAsync(
        RavenDBCloudDeployment deployment,
        RavenDBCloudPlan plan,
        string productId,
        CancellationToken cancellationToken)
    {
        ProductDetails? details = null;

        if (plan.InstanceTypeChange is { } instanceType)
        {
            logger.LogInformation("Changing the instance type of RavenDB Cloud product '{Product}' to {InstanceType}.", deployment.ProductName, instanceType);
            await client.ChangeInstanceTypeAsync(productId, instanceType, cancellationToken).ConfigureAwait(false);
            details = await WaitForActiveAsync(deployment, productId, null, cancellationToken, afterChange: true).ConfigureAwait(false);
        }

        if (plan.StorageChange is { } storage)
        {
            logger.LogInformation(
                "Changing the storage of RavenDB Cloud product '{Product}' to {Size} GB {Type}.",
                deployment.ProductName,
                storage.Size,
                storage.StorageType);
            await client.ChangeStorageAsync(storage, cancellationToken).ConfigureAwait(false);
            details = await WaitForActiveAsync(deployment, productId, null, cancellationToken, afterChange: true).ConfigureAwait(false);
        }

        // Read back: the deployment must not report success for a product that is not what the AppHost asked for.
        var spec = deployment.Spec!;
        var actual = details!;

        if ((plan.InstanceTypeChange is not null && !string.Equals(actual.InstanceType, spec.InstanceType, StringComparison.OrdinalIgnoreCase)) ||
            (plan.StorageChange is not null && actual.HardwareInfo?.DataDisk is { } s &&
             (s.Size != spec.DiskSizeInGb || !string.Equals(s.Type, spec.StorageType.ToString(), StringComparison.OrdinalIgnoreCase))))
        {
            throw new InvalidOperationException(
                $"RavenDB Cloud product '{deployment.ProductName}' is {RavenDBCloudPlanner.Describe(actual)} after the change, not {spec}. " +
                "See the portal for details.");
        }

        return actual;
    }

    private async Task<ProductDetails> WaitForActiveAsync(
        RavenDBCloudDeployment deployment,
        string productId,
        ProductDetails? details,
        CancellationToken cancellationToken,
        bool afterChange = false)
    {
        var started = DateTimeOffset.UtcNow;
        var deadline = started + deployment.ProvisioningTimeout;
        string? lastStatus = null;

        // Right after a change request the product may still report Active for a moment: wait until it reports the
        // change, or until it had time to.
        var sawChange = !afterChange;

        while (true)
        {
            details ??= await client.GetProductAsync(productId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"RavenDB Cloud product '{deployment.ProductName}' disappeared.");

            if (!string.Equals(details.Status, lastStatus, StringComparison.Ordinal))
            {
                logger.LogInformation("RavenDB Cloud product '{Product}': {Status}.", deployment.ProductName, details.Status);
                lastStatus = details.Status;
            }

            switch (details.Status)
            {
                case ProductStatus.Active when sawChange || DateTimeOffset.UtcNow - started > deployment.ChangeGracePeriod:
                    return details;

                case ProductStatus.Creating or ProductStatus.DeployingChanges:
                    sawChange = true;
                    break;

                case ProductStatus.Error:
                    throw new InvalidOperationException($"RavenDB Cloud product '{deployment.ProductName}' is in error. See the portal for details.");

                case ProductStatus.AwaitingPayment:
                    throw new InvalidOperationException(
                        $"RavenDB Cloud product '{deployment.ProductName}' is awaiting payment. Complete it in the portal and deploy again.");

                case ProductStatus.Terminating or ProductStatus.Terminated:
                    throw new InvalidOperationException($"RavenDB Cloud product '{deployment.ProductName}' is being terminated.");
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException(
                    $"RavenDB Cloud product '{deployment.ProductName}' was still '{details.Status}' after {deployment.ProvisioningTimeout}.");
            }

            // The Cloud API accepts about one request per second.
            await Task.Delay(deployment.PollInterval, cancellationToken).ConfigureAwait(false);
            details = null;
        }
    }

    /// <summary>The URLs of the product's nodes; the API lists host names without a scheme.</summary>
    internal static IReadOnlyList<string> GetNodeUrls(ProductDetails details) =>
        details.Dns?
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => (d.Contains("://", StringComparison.Ordinal) ? d : "https://" + d).TrimEnd('/'))
            .ToList() ?? [];

    /// <summary>Takes the product's id, size and node URLs from its details.</summary>
    private static void Resolve(RavenDBCloudDeployment deployment, string productId, ProductDetails details)
    {
        var urls = GetNodeUrls(details);

        if (urls.Count == 0)
        {
            throw new InvalidOperationException($"RavenDB Cloud product '{deployment.ProductName}' is active but reports no URL.");
        }

        deployment.ProductId = productId;
        deployment.NodeCount = Math.Max(details.NodeTags?.Count ?? 1, 1);
        deployment.NodeUrls = urls;

        // Every node, so that an application reaches the cluster while one node is down.
        deployment.Endpoint.Value = string.Join(",", urls);
    }

    private static bool IsGone(string? status) => status is ProductStatus.Terminated or ProductStatus.Terminating;
}
