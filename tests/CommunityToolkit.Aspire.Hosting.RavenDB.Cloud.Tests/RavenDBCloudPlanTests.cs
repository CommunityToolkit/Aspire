using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;

namespace CommunityToolkit.Aspire.Hosting.RavenDB.Cloud.Tests;

public class RavenDBCloudPlanTests
{
    private static readonly RavenDBCloudSpec s_dev10 =
        RavenDBCloudSpec.Development(RavenDBCloudProvider.Aws, "eu-central-1", "Dev10", 20, RavenDBCloudStorageType.SsdStandard, RavenDBCloudDiskLayout.RootOnly, "Stable72");

    private static readonly RavenDBCloudSpec s_dev10DataDisk =
        RavenDBCloudSpec.Development(RavenDBCloudProvider.Aws, "eu-central-1", "Dev10", 20, RavenDBCloudStorageType.SsdStandard, RavenDBCloudDiskLayout.SingleDataDisk, "Stable72");

    internal static RavenDBCloudDeployment Managed(RavenDBCloudSpec? spec = null, Action<RavenDBCloudOptions>? configure = null, string name = "shop-prod")
    {
        var options = new RavenDBCloudOptions().WithAllowedIps("203.0.113.0/24");
        configure?.Invoke(options);
        return new RavenDBCloudDeployment(Server(), ApiKey(), RavenDBCloudMode.Managed, name, spec ?? s_dev10, null, options) { PollInterval = TimeSpan.Zero, ChangeGracePeriod = TimeSpan.Zero };
    }

    internal static RavenDBCloudDeployment Attached(string clientName = "billing", Action<RavenDBCloudOptions>? configure = null)
    {
        var options = new RavenDBCloudOptions();
        configure?.Invoke(options);
        return new RavenDBCloudDeployment(Server(), ApiKey(), RavenDBCloudMode.Attached, "shared", null, clientName, options) { PollInterval = TimeSpan.Zero, ChangeGracePeriod = TimeSpan.Zero };
    }

    /// <summary>A server that declares the database <c>orders</c>, as the AppHost in most tests does.</summary>
    internal static RavenDBServerResource Server()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var server = builder.AddRavenDB("ravendb");
        server.AddDatabase("orders");
        return server.Resource;
    }

    internal static ParameterResource ApiKey() => new("api-key", _ => "key", secret: true);

    private static async Task<RavenDBCloudPlan> PlanAsync(FakeRavenDBCloudApi api, RavenDBCloudDeployment deployment, bool approved = false, IReadOnlyList<RavenDBConsumer>? consumers = null)
    {
        using var client = api.Create("https://api.test", "key");
        return await RavenDBCloudPlanner.PlanAsync(client, deployment, consumers ?? [], approved, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MissingProductIsCreatedFromTheSpec()
    {
        var plan = await PlanAsync(new FakeRavenDBCloudApi(), Managed(configure: o => o.Subdomain = "shop"));

        Assert.False(plan.Blocks);
        Assert.Equal(
            "create 'shop-prod': Development Dev10, Aws eu-central-1, 20 GB SsdStandard on RootOnly, channel Stable72, allowed IPs 203.0.113.0/24",
            Assert.Single(plan.Lines).Text);

        var create = plan.Create!;
        Assert.Equal(("Aws", "eu-central-1", "Dev10", "Development", 20, "SsdStandard", "Stable72", "shop"),
            (create.CloudProvider, create.Region, create.InstanceTypeName, create.Tier, create.DiskSize, create.StorageTypeName, create.ReleaseChannel, create.SubdomainName));
        Assert.Equal(["203.0.113.0/24"], create.AllowedIps);
        Assert.Equal("RootOnly", create.DiskLayout);
    }

    [Fact]
    public async Task CreatingWithoutAllowedIpsStops()
    {
        var deployment = new RavenDBCloudDeployment(Server(), ApiKey(), RavenDBCloudMode.Managed, "shop-prod", s_dev10, null, new RavenDBCloudOptions());

        var plan = await PlanAsync(new FakeRavenDBCloudApi(), deployment);

        Assert.True(plan.Blocks);
        Assert.Contains(plan.Lines, l => l.Blocks && l.Text.Contains("at least one allowed IP range", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("eu-west-9", "Dev10", 20, "region 'eu-west-9' is not offered on Aws. Offered: us-east-1, eu-central-1.")]
    [InlineData("eu-central-1", "Dev99", 20, "instance type 'Dev99' is not offered for the Development tier in Aws eu-central-1. Offered: Dev10 (2 vCPU, 2 GB RAM, 1 node, disks 10/20/32 GB), Dev20 (2 vCPU, 4 GB RAM, 1 node, disks 20/32/64 GB).")]
    [InlineData("eu-central-1", "P20", 64, "instance type 'P20' is not offered for the Development tier in Aws eu-central-1. Offered: Dev10 (2 vCPU, 2 GB RAM, 1 node, disks 10/20/32 GB), Dev20 (2 vCPU, 4 GB RAM, 1 node, disks 20/32/64 GB).")]
    [InlineData("eu-central-1", "Dev10", 25, "disk size 25 GB is not offered for instance type 'Dev10'. Offered: 10, 20, 32 GB.")]
    public async Task SpecOutsideTheOfferStopsAndListsWhatIsOffered(string region, string instanceType, int disk, string expected)
    {
        var spec = RavenDBCloudSpec.Development(RavenDBCloudProvider.Aws, region, instanceType, disk, RavenDBCloudStorageType.SsdStandard, RavenDBCloudDiskLayout.RootOnly, "Stable72");

        var plan = await PlanAsync(new FakeRavenDBCloudApi(), Managed(spec));

        Assert.Contains(plan.Lines, l => l.Blocks && l.Text == expected);
    }

    [Fact]
    public async Task UnknownReleaseChannelAndIopsOutOfRangeStop()
    {
        var spec = RavenDBCloudSpec.Development(RavenDBCloudProvider.Aws, "eu-central-1", "Dev10", 20, RavenDBCloudStorageType.SsdPremium, RavenDBCloudDiskLayout.RootOnly, "Nightly", iops: 100);

        var plan = await PlanAsync(new FakeRavenDBCloudApi(), Managed(spec));

        Assert.Contains(plan.Lines, l => l.Blocks && l.Text == "100 IOPS is outside the range offered for SsdPremium: 3000 to 16000.");
        Assert.Contains(plan.Lines, l => l.Blocks && l.Text == "release channel 'Nightly' does not exist. Available: Stable72 (RavenDB 7.2.7), Stable62 (RavenDB 6.2.20).");
    }

    [Fact]
    public async Task UnknownReleaseChannelShowsNextToAnUnknownInstanceType()
    {
        var spec = RavenDBCloudSpec.Development(RavenDBCloudProvider.Aws, "eu-central-1", "Dev99", 20, RavenDBCloudStorageType.SsdStandard, RavenDBCloudDiskLayout.RootOnly, "Nightly");

        var plan = await PlanAsync(new FakeRavenDBCloudApi(), Managed(spec));

        Assert.Contains(plan.Lines, l => l.Blocks && l.Text.StartsWith("instance type 'Dev99' is not offered", StringComparison.Ordinal));
        Assert.Contains(plan.Lines, l => l.Blocks && l.Text.StartsWith("release channel 'Nightly' does not exist", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProductThatMatchesHasNoChanges()
    {
        var api = new FakeRavenDBCloudApi();
        api.AddProduct("shop-prod");

        var plan = await PlanAsync(api, Managed());

        Assert.False(plan.Blocks);
        Assert.False(plan.HasChanges);
        Assert.StartsWith("no changes: 'shop-prod'", Assert.Single(plan.Lines).Text, StringComparison.Ordinal);
    }

    /// <summary>The spec describes the disk the data is on, not the system disk of a product with a data disk.</summary>
    [Fact]
    public async Task ProductWithADataDiskIsComparedByItsDataDisk()
    {
        var api = new FakeRavenDBCloudApi();
        api.AddProduct("shop-prod").HasDataDisk = true;

        var plan = await PlanAsync(api, Managed(s_dev10DataDisk));

        Assert.False(plan.Blocks);
        Assert.False(plan.HasChanges);
        Assert.StartsWith("no changes: 'shop-prod'", Assert.Single(plan.Lines).Text, StringComparison.Ordinal);
    }

    /// <summary>The Cloud API cannot move a product's data to another disk layout.</summary>
    [Theory]
    [InlineData(false, "disk layout RootOnly -> SingleDataDisk")]
    [InlineData(true, "disk layout SingleDataDisk -> RootOnly")]
    public async Task OtherDiskLayoutIsImpossible(bool productHasDataDisk, string expected)
    {
        var api = new FakeRavenDBCloudApi();
        api.AddProduct("shop-prod").HasDataDisk = productHasDataDisk;

        var plan = await PlanAsync(api, Managed(productHasDataDisk ? s_dev10 : s_dev10DataDisk), approved: true);

        Assert.Contains(plan.Lines, l => l.Blocks && l.Text.StartsWith($"impossible: {expected}.", StringComparison.Ordinal));
    }

    /// <summary>A product RavenDB Cloud could not set up stops the plan before any change is planned for it.</summary>
    [Theory]
    [InlineData("Error", "error: 'shop-prod' is in error in RavenDB Cloud.")]
    [InlineData("AwaitingPayment", "awaiting payment: 'shop-prod' waits for a payment.")]
    public async Task ProductRavenDBCloudCannotServeStops(string status, string expected)
    {
        var api = new FakeRavenDBCloudApi();
        api.AddProduct("shop-prod", status: status);
        var bigger = RavenDBCloudSpec.Development(RavenDBCloudProvider.Aws, "eu-central-1", "Dev20", 32, RavenDBCloudStorageType.SsdStandard, RavenDBCloudDiskLayout.RootOnly, "Stable72");

        var plan = await PlanAsync(api, Managed(bigger), approved: true);

        Assert.StartsWith(expected, Assert.Single(plan.Lines, l => l.Blocks).Text, StringComparison.Ordinal);
        Assert.False(plan.HasChanges);
    }

    [Fact]
    public async Task ChangesStopWithoutApproval()
    {
        var api = new FakeRavenDBCloudApi();
        api.AddProduct("shop-prod");
        var spec = RavenDBCloudSpec.Development(RavenDBCloudProvider.Aws, "eu-central-1", "Dev20", 32, RavenDBCloudStorageType.SsdStandard, RavenDBCloudDiskLayout.RootOnly, "Stable72");

        var plan = await PlanAsync(api, Managed(spec));

        Assert.True(plan.Blocks);
        Assert.False(plan.HasChanges);
        Assert.Equal(
            "change 'shop-prod': instance Dev10 -> Dev20, disk 20 GB -> 32 GB. Deploy with RavenDB__Cloud__ApproveChanges=true to apply it.",
            Assert.Single(plan.Lines).Text);
    }

    [Fact]
    public async Task ApprovedChangesArePlanned()
    {
        var api = new FakeRavenDBCloudApi();
        api.AddProduct("shop-prod");
        var spec = RavenDBCloudSpec.Development(RavenDBCloudProvider.Aws, "eu-central-1", "Dev20", 32, RavenDBCloudStorageType.SsdPremium, RavenDBCloudDiskLayout.RootOnly, "Stable72", iops: 3000);

        var plan = await PlanAsync(api, Managed(spec), approved: true);

        Assert.False(plan.Blocks);
        Assert.Equal(
            "change 'shop-prod': instance Dev10 -> Dev20, disk 20 GB -> 32 GB, storage SsdStandard -> SsdPremium, IOPS default -> 3000 " +
            "(approved by RavenDB__Cloud__ApproveChanges)",
            Assert.Single(plan.Lines).Text);
        Assert.Equal("Dev20", plan.InstanceTypeChange);
        Assert.Equal((32, "SsdPremium", 3000), (plan.StorageChange!.Size, plan.StorageChange.StorageType, plan.StorageChange.Iops));
    }

    [Fact]
    public async Task ImpossibleChangesStopEvenWhenApproved()
    {
        var api = new FakeRavenDBCloudApi();
        var product = api.AddProduct("shop-prod");
        product.DiskSize = 32;
        product.ReleaseChannel = "Stable62";
        var spec = RavenDBCloudSpec.Development(RavenDBCloudProvider.Azure, "westeurope", "Dev10", 20, RavenDBCloudStorageType.SsdStandard, RavenDBCloudDiskLayout.RootOnly, "Stable72");

        var plan = await PlanAsync(api, Managed(spec), approved: true);

        Assert.True(plan.Blocks);
        var texts = plan.Lines.Where(l => l.Blocks).Select(l => l.Text).ToList();
        Assert.Contains(texts, t => t.StartsWith("impossible: provider Aws -> Azure.", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.StartsWith("impossible: region eu-central-1 -> westeurope.", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.StartsWith("impossible: release channel Stable62 -> Stable72 (change it in the portal).", StringComparison.Ordinal));
        Assert.Contains(texts, t => t == "impossible: disk 32 GB -> 20 GB. A disk cannot shrink, and RavenDB Cloud grows one that fills up: set the disk size in the AppHost to 32 GB.");
    }

    [Fact]
    public async Task AnotherTierIsImpossible()
    {
        var api = new FakeRavenDBCloudApi();
        api.AddProduct("shop-prod");
        var spec = RavenDBCloudSpec.Production(RavenDBCloudProvider.Aws, "eu-central-1", "P20", 64, RavenDBCloudStorageType.SsdStandard, RavenDBCloudDiskLayout.RootOnly, "Stable72");

        var plan = await PlanAsync(api, Managed(spec), approved: true);

        Assert.True(plan.Blocks);
        Assert.Null(plan.InstanceTypeChange);
        Assert.Contains(
            plan.Lines,
            l => l.Blocks && l.Text.StartsWith("impossible: tier Development -> Production (RavenDB Cloud cannot change the tier of a product).", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TwoLiveProductsWithTheNameStop()
    {
        var api = new FakeRavenDBCloudApi();
        api.AddProduct("shop-prod");
        api.AddProduct("SHOP-PROD");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => PlanAsync(api, Managed()));

        Assert.Contains("2 RavenDB Cloud products named 'shop-prod'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TerminatedProductStillHoldsTheNameUntilItIsHidden()
    {
        var api = new FakeRavenDBCloudApi();
        var terminated = api.AddProduct("shop-prod", status: "Terminated");

        var plan = await PlanAsync(api, Managed());

        Assert.Null(plan.Create);
        Assert.Contains(plan.Lines, l => l.Blocks && l.Text.StartsWith("name: 'shop-prod' is held by a terminated product", StringComparison.Ordinal));

        terminated.Hidden = true;
        Assert.NotNull((await PlanAsync(api, Managed())).Create);
    }

    [Fact]
    public async Task ProductStillBeingTerminatedHoldsTheNameForAWhile()
    {
        var api = new FakeRavenDBCloudApi();
        api.AddProduct("shop-prod", status: "Terminating");

        var plan = await PlanAsync(api, Managed());

        Assert.Null(plan.Create);
        Assert.Contains(plan.Lines, l => l.Blocks && l.Text == "name: 'shop-prod' is held by a product that is still being terminated. Deploy again once RavenDB Cloud has terminated it.");
    }

    [Fact]
    public async Task AttachedProductThatIsMissingStops()
    {
        var plan = await PlanAsync(new FakeRavenDBCloudApi(), Attached());

        Assert.True(plan.Blocks);
        Assert.StartsWith("missing: there is no RavenDB Cloud product 'shared'.", Assert.Single(plan.Lines).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AttachedProductIsNeverChanged()
    {
        var api = new FakeRavenDBCloudApi();
        api.AddProduct("shared");

        var plan = await PlanAsync(api, Attached(), approved: true);

        Assert.False(plan.Blocks);
        Assert.False(plan.HasChanges);
        Assert.Null(plan.Create);
        Assert.StartsWith("attach: 'shared'", Assert.Single(plan.Lines).Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("203.0.113.0/24", "203.0.113.7/32", false)]
    [InlineData("203.0.113.0/24", "203.0.113.0/25", false)]
    [InlineData("203.0.113.0/24", "198.51.100.0/24", true)]
    [InlineData("203.0.113.0/24", "203.0.112.0/23", true)]
    [InlineData("0.0.0.0/0", "198.51.100.0/24", false)]
    [InlineData("2001:db8::/32", "2001:db8:1::/48", false)]
    [InlineData("2001:db8::/32", "10.0.0.1/32", true)]
    public async Task AllowedIpsMustBeCoveredByTheProduct(string allowedOnProduct, string range, bool blocks)
    {
        var api = new FakeRavenDBCloudApi();
        api.AddProduct("shared").AllowedIps = [allowedOnProduct];

        var plan = await PlanAsync(api, Attached(configure: o => o.WithAllowedIps(range)));

        Assert.Equal(blocks, plan.Blocks);

        if (blocks)
        {
            Assert.Contains(plan.Lines, l => l.Text.StartsWith($"allowed IPs: {range} not allowed on 'shared'.", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task ApplicationInAnUnsupportedEnvironmentStops()
    {
        var api = new FakeRavenDBCloudApi();
        api.AddProduct("shop-prod");
        var application = new ContainerResource("api");
        application.Annotations.Add(new DeploymentTargetAnnotation(application) { ComputeEnvironment = new UnsupportedEnvironment("k8s") });

        var plan = await PlanAsync(api, Managed(), consumers: [new RavenDBConsumer(application, ["ravendb"], [], 1)]);

        Assert.Contains(plan.Lines, l => l.Blocks && l.Text ==
            "no target: 'api' is deployed to 'k8s', where the RavenDB Cloud integration cannot deliver the URL and certificate of " +
            "'ravendb'. Supported: Docker Compose, as an application's only compute environment.");
    }

    /// <summary>
    /// Azure Container Apps would carry the certificate as a plain template parameter. Checked with the real environment,
    /// whose type name contains no "DockerCompose".
    /// </summary>
    [Fact]
    public async Task ApplicationInAzureContainerAppsStops()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var aca = builder.AddAzureContainerAppEnvironment("aca");
        var api = new FakeRavenDBCloudApi();
        api.AddProduct("shop-prod");

        var plan = await PlanAsync(api, Managed(), consumers: [Consumer(aca.Resource)]);

        Assert.Contains(plan.Lines, l => l.Blocks && l.Text.StartsWith("no target: 'api' is deployed to 'aca'", StringComparison.Ordinal));
    }

    /// <summary>The environment is recognized by its full type name, not by a class that only shares the short name.</summary>
    [Fact]
    public async Task EnvironmentThatOnlySharesTheNameStops()
    {
        var api = new FakeRavenDBCloudApi();
        api.AddProduct("shop-prod");

        var plan = await PlanAsync(api, Managed(), consumers: [Consumer(new DockerComposeEnvironmentResource("compose"))]);

        Assert.Contains(plan.Lines, l => l.Blocks && l.Text.StartsWith("no target: 'api' is deployed to 'compose'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ApplicationWithoutOrWithTwoEnvironmentsStops()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var compose = builder.AddDockerComposeEnvironment("compose");
        var api = new FakeRavenDBCloudApi();
        api.AddProduct("shop-prod");
        var nowhere = new ContainerResource("nowhere");
        var twice = Consumer(compose.Resource, "twice").Resource;
        twice.Annotations.Add(new DeploymentTargetAnnotation(twice) { ComputeEnvironment = new UnsupportedEnvironment("k8s") });

        var plan = await PlanAsync(
            api,
            Managed(),
            consumers: [new RavenDBConsumer(nowhere, ["ravendb"], [], 1), new RavenDBConsumer(twice, ["ravendb"], [], 1), Consumer(compose.Resource, "fine")]);

        Assert.Contains(plan.Lines, l => l.Blocks && l.Text.StartsWith("no target: 'nowhere' is deployed to no compute environment", StringComparison.Ordinal));
        Assert.Contains(plan.Lines, l => l.Blocks && l.Text.StartsWith("no target: 'twice' is deployed to 2 compute environments", StringComparison.Ordinal));
        Assert.DoesNotContain(plan.Lines, l => l.Text.StartsWith("no target: 'fine'", StringComparison.Ordinal));
    }

    private static RavenDBConsumer Consumer(IComputeEnvironmentResource environment, string name = "api")
    {
        var application = new ContainerResource(name);
        application.Annotations.Add(new DeploymentTargetAnnotation(application) { ComputeEnvironment = environment });
        return new RavenDBConsumer(application, ["ravendb"], [], 1);
    }

    [Theory]
    [InlineData("0.0.0.0/0", true)]
    [InlineData("10.0.0.0/8", true)]
    [InlineData("10.1.0.0/16", false)]
    [InlineData("2001:db8::/32", true)]
    [InlineData("2001:db8:1::/48", false)]
    public async Task WideAllowedIpRangeIsAWarning(string range, bool warns)
    {
        var api = new FakeRavenDBCloudApi();
        api.AddProduct("shared").AllowedIps = ["0.0.0.0/0", "::/0"];

        var plan = await PlanAsync(api, Attached(configure: o => o.WithAllowedIps(range)));

        Assert.False(plan.Blocks);
        Assert.Equal(warns, plan.Lines.Any(l => l.Text.StartsWith($"wide allowed IPs: {range} lets a large part of the internet reach 'shared'", StringComparison.Ordinal)));
    }

    /// <summary>The CIDR notation RavenDB Cloud takes; a bare address it refuses too.</summary>
    [Theory]
    [InlineData("203.0.113.7")]
    [InlineData("not-an-ip")]
    [InlineData("10.0.0.0/33")]
    [InlineData("10.0.0.0/x")]
    public async Task AllowedIpRangeThatIsNotCidrStops(string range)
    {
        var api = new FakeRavenDBCloudApi();
        api.AddProduct("shared");

        var plan = await PlanAsync(api, Attached(configure: o => o.WithAllowedIps(range)));

        Assert.Contains(plan.Lines, l => l.Blocks && l.Text.StartsWith($"allowed IP range '{range}' is not a valid CIDR range", StringComparison.Ordinal));
        Assert.DoesNotContain(plan.Lines, l => l.Text.StartsWith("allowed IPs:", StringComparison.Ordinal));
    }

    private sealed class UnsupportedEnvironment(string name) : Resource(name), IComputeEnvironmentResource;

    private sealed class DockerComposeEnvironmentResource(string name) : Resource(name), IComputeEnvironmentResource;
}
