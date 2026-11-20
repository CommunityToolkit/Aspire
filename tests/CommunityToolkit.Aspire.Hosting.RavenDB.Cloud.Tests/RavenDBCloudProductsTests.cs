using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging.Abstractions;
using Raven.Client.ServerWide.Operations.Certificates;
using static CommunityToolkit.Aspire.Hosting.RavenDB.Cloud.Tests.RavenDBCloudPlanTests;

namespace CommunityToolkit.Aspire.Hosting.RavenDB.Cloud.Tests;

public class RavenDBCloudProductsTests
{
    private readonly FakeRavenDBCloudApi _api = new();

    private async Task<RavenDBCloudPlan> ProvisionAsync(RavenDBCloudDeployment deployment, bool approved = false)
    {
        using var client = _api.Create("https://api.test", "key");
        var plan = await RavenDBCloudPlanner.PlanAsync(client, deployment, [], approved, TestContext.Current.CancellationToken);
        await new RavenDBCloudProducts(client, NullLogger.Instance).ProvisionAsync(deployment, plan, TestContext.Current.CancellationToken);
        return plan;
    }

    private async Task<ProductDetails?> DestroyAsync(RavenDBCloudDeployment deployment)
    {
        using var client = _api.Create("https://api.test", "key");
        return await new RavenDBCloudProducts(client, NullLogger.Instance).DestroyAsync(deployment, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreatesTheProductWaitsUntilActiveAndResolvesItsUrl()
    {
        var deployment = Managed();

        await ProvisionAsync(deployment);

        var product = Assert.Single(_api.Products.Values);
        Assert.Equal(("shop-prod", "Active"), (product.Name, product.Status));
        Assert.Equal("https://a.shop-prod.development.run", deployment.Endpoint.Value);
        Assert.Equal(product.Id, deployment.ProductId);
    }

    /// <summary>Every node, so that an application reaches the cluster while one node is down.</summary>
    [Fact]
    public async Task TheApplicationsGetEveryNodeOfTheCluster()
    {
        _api.AddProduct("shop-prod").Dns = ["a.shop-prod.development.run", "b.shop-prod.development.run", "c.shop-prod.development.run"];
        var deployment = Managed();

        await ProvisionAsync(deployment);

        Assert.Equal("https://a.shop-prod.development.run,https://b.shop-prod.development.run,https://c.shop-prod.development.run", deployment.Endpoint.Value);
        Assert.Equal(3, deployment.NodeUrls.Count);
    }

    [Fact]
    public async Task BlockedPlanChangesNothing()
    {
        _api.AddProduct("shop-prod");
        var spec = RavenDBCloudSpec.Development(RavenDBCloudProvider.Aws, "eu-central-1", "Dev20", 32, RavenDBCloudStorageType.SsdStandard, RavenDBCloudDiskLayout.RootOnly, "Stable72");

        await Assert.ThrowsAsync<InvalidOperationException>(() => ProvisionAsync(Managed(spec)));

        Assert.Empty(_api.ChangeRequests);
        Assert.DoesNotContain(_api.Requests, r => r.StartsWith("POST", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AppliesApprovedChangesAndWaitsForThem()
    {
        _api.AddProduct("shop-prod");
        var spec = RavenDBCloudSpec.Development(RavenDBCloudProvider.Aws, "eu-central-1", "Dev20", 32, RavenDBCloudStorageType.SsdStandard, RavenDBCloudDiskLayout.RootOnly, "Stable72");

        await ProvisionAsync(Managed(spec), approved: true);

        Assert.Equal(2, _api.ChangeRequests.Count);
        var product = Assert.Single(_api.Products.Values);
        Assert.Equal(("Dev20", 32, "Active"), (product.InstanceType, product.DiskSize, product.Status));
    }

    [Fact]
    public async Task ExistingProductIsFoundByName()
    {
        var existing = _api.AddProduct("shop-prod");
        var deployment = Managed();

        await ProvisionAsync(deployment);

        Assert.Equal(existing.Id, deployment.ProductId);
        Assert.Empty(_api.CreateRequests);
    }

    [Fact]
    public async Task DestroyTerminatesTheProductWhenAsked()
    {
        var deployment = Managed(configure: o => o.TerminateOnDestroy = true);
        await ProvisionAsync(deployment);

        Assert.Null(await DestroyAsync(deployment));

        Assert.Equal(_api.Products.Keys.Single(), Assert.Single(_api.TerminatedProductIds));

        // Hidden as well, so the next deployment can create a product with the name again.
        Assert.True(_api.Products.Values.Single().Hidden);
        await ProvisionAsync(Managed(configure: o => o.TerminateOnDestroy = true));
        Assert.Equal(2, _api.Products.Count);
    }

    /// <summary>The name is the identity: from a CI runner, or for a product made in the portal, destroy finds it too.</summary>
    [Fact]
    public async Task DestroyTerminatesAProductItFindsByName()
    {
        var product = _api.AddProduct("shop-prod");

        Assert.Null(await DestroyAsync(Managed(configure: o => o.TerminateOnDestroy = true)));

        Assert.Equal(product.Id, Assert.Single(_api.TerminatedProductIds));
    }

    /// <summary>One name in two environments: destroy must not terminate the product the other one describes.</summary>
    [Theory]
    [InlineData("tier")]
    [InlineData("region")]
    [InlineData("instance type")]
    public async Task DestroyRefusesAProductTheAppHostDoesNotDescribe(string difference)
    {
        var product = _api.AddProduct("shop-prod");
        _ = difference switch
        {
            "tier" => product.Tier = "Production",
            "region" => product.Region = "us-east-1",
            _ => product.InstanceType = "Dev20",
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => DestroyAsync(Managed(configure: o => o.TerminateOnDestroy = true)));

        Assert.Contains("does not match the AppHost", exception.Message, StringComparison.Ordinal);
        Assert.Contains(difference, exception.Message, StringComparison.Ordinal);
        Assert.Empty(_api.TerminatedProductIds);
    }

    /// <summary>A disk that RavenDB Cloud grew, or a subdomain chosen in the portal, still describe the product the AppHost means.</summary>
    [Fact]
    public async Task DestroyIgnoresTheDiskAndTheSubdomain()
    {
        var product = _api.AddProduct("shop-prod");
        product.DiskSize = 64;
        product.Subdomain = "shop";

        Assert.Null(await DestroyAsync(Managed(configure: o => o.TerminateOnDestroy = true)));
        Assert.Single(_api.TerminatedProductIds);
    }

    [Fact]
    public async Task DestroyLeavesItRunningWithoutTerminateOnDestroy()
    {
        var deployment = Managed();
        await ProvisionAsync(deployment);

        Assert.NotNull(await DestroyAsync(deployment));
        Assert.Empty(_api.TerminatedProductIds);
    }

    [Fact]
    public async Task DestroyTerminatesAProductThatIsStillBeingCreated()
    {
        var deployment = Managed(configure: o => o.TerminateOnDestroy = true);
        _api.BusyPolls = int.MaxValue;
        using (var client = _api.Create("https://api.test", "key"))
        {
            var plan = await RavenDBCloudPlanner.PlanAsync(client, deployment, [], false, TestContext.Current.CancellationToken);
            deployment.ProvisioningTimeout = TimeSpan.Zero;
            await Assert.ThrowsAsync<TimeoutException>(() =>
                new RavenDBCloudProducts(client, NullLogger.Instance).ProvisionAsync(deployment, plan, TestContext.Current.CancellationToken));
        }

        Assert.Null(await DestroyAsync(deployment));
        Assert.Single(_api.TerminatedProductIds);
    }

    [Fact]
    public async Task AttachedProductIsNeverTerminated()
    {
        _api.AddProduct("shared");

        Assert.NotNull(await DestroyAsync(Attached()));
        Assert.Empty(_api.TerminatedProductIds);
    }
}

public class RavenDBCloudCertificatesTests
{
    /// <summary>
    /// The deployment reuses one admin certificate for every connection, and the RavenDB client keeps it in its HTTP
    /// client cache: closing a connection must leave it usable (a disposed one fails with an invalid handle).
    /// </summary>
    [Fact]
    public void ClosingAConnectionLeavesTheAdminCertificateUsable()
    {
        using var certificate = RavenDBCloudCertificates.Load(TestCertificates.CreatePfx("admin"));

        new RavenDBServerAdministration(["https://127.0.0.1:1"], certificate).Dispose();

        Assert.NotEmpty(certificate.Thumbprint);
    }

    private readonly FakeRavenDBServer _server = new();
    private readonly RavenDBCloudCertificates _certificates = new(NullLogger.Instance) { RevokeRetryDelay = TimeSpan.Zero };

    private static (RavenDBCloudDeployment Deployment, List<RavenDBConsumer> Consumers) WithApplications(RavenDBCloudDeployment deployment, params string[] applications)
    {
        var consumers = new List<RavenDBConsumer>();

        foreach (var name in applications)
        {
            var application = new ContainerResource(name);
            deployment.Certificates.Add(new RavenDBCloudCertificate(deployment.Server, application));
            consumers.Add(new RavenDBConsumer(application, ["orders"], ["orders"], 1));
        }

        return (deployment, consumers);
    }

    private IRavenDBServerAdministration Session() => _server.Create(["https://a.test"], null);

    [Fact]
    public async Task IssuesEachApplicationANewCertificateWithAdminOnItsDatabases()
    {
        var (deployment, consumers) = WithApplications(Managed(), "api", "worker");

        await _certificates.IssueAsync(Session(), deployment, consumers, TestContext.Current.CancellationToken);

        Assert.All(deployment.Certificates, c =>
        {
            var pfx = Convert.FromBase64String(c.Value!);
            Assert.Equal(c.Thumbprint, RavenDBCloudCertificates.GetThumbprint(pfx));

            var registered = _server.Certificates[c.Thumbprint!];
            Assert.Equal($"aspire.{c.Consumer.Name}", registered.Name);
            Assert.Equal(DatabaseAccess.Admin, Assert.Single(registered.Permissions, p => p.Key == "orders").Value);
        });
    }

    [Fact]
    public async Task RevokesReplacedCertificatesButNotOtherClients()
    {
        var previous = _server.Register("aspire.api", "orders");
        var removedApplication = _server.Register("aspire.reports", "orders");
        var otherClient = _server.Register("aspire.billing.api", "orders");
        var unrelated = _server.Register("john", "orders");
        var (deployment, consumers) = WithApplications(Managed(), "api");

        await _certificates.IssueAsync(Session(), deployment, consumers, TestContext.Current.CancellationToken);
        await _certificates.RevokeReplacedAsync(Session(), deployment, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { previous, removedApplication }.Order(), _server.DeletedThumbprints.Order());
        Assert.Contains(otherClient, _server.Certificates.Keys);
        Assert.Contains(unrelated, _server.Certificates.Keys);
        Assert.Contains(deployment.Certificates.Single().Thumbprint, _server.Certificates.Keys);
    }

    [Fact]
    public async Task AttachedClientRevokesOnlyItsOwnCertificates()
    {
        var own = _server.Register("aspire.billing.old-api", "orders");
        var managedOwner = _server.Register("aspire.api", "orders");
        var otherClient = _server.Register("aspire.shop.api", "orders");
        var (deployment, consumers) = WithApplications(Attached("billing"), "api");

        await _certificates.IssueAsync(Session(), deployment, consumers, TestContext.Current.CancellationToken);
        await _certificates.RevokeReplacedAsync(Session(), deployment, TestContext.Current.CancellationToken);

        Assert.Equal("aspire.billing.api", _server.Certificates[deployment.Certificates.Single().Thumbprint!].Name);
        Assert.Equal([own], _server.DeletedThumbprints);
        Assert.Contains(managedOwner, _server.Certificates.Keys);
        Assert.Contains(otherClient, _server.Certificates.Keys);
    }

    /// <summary>
    /// Only what this AppHost issues is revoked: a certificate that shares the name but has more clearance, or access to a
    /// database the AppHost does not declare, was made by someone else.
    /// </summary>
    [Fact]
    public async Task CertificatesThatOnlyShareTheNameAreNotRevoked()
    {
        var issued = _server.Register("aspire.api", "orders");
        var operatorOne = _server.Register("aspire.ops", SecurityClearance.Operator, new());
        var otherDatabase = _server.Register("aspire.reports", "invoices");
        var readOnly = _server.Register("aspire.worker", SecurityClearance.ValidUser, new() { ["orders"] = DatabaseAccess.Read });
        var deployment = Managed();

        await _certificates.RevokeAllAsync(Session(), deployment, TestContext.Current.CancellationToken);

        Assert.Equal([issued], _server.DeletedThumbprints);
        Assert.Equal(new[] { operatorOne, otherDatabase, readOnly }.Order(), _server.Certificates.Keys.Order());

        var others = await RavenDBCloudCertificates.GetOtherClientsAsync(Session(), deployment, TestContext.Current.CancellationToken);
        Assert.Equal(["aspire.ops", "aspire.reports", "aspire.worker"], others);
    }

    [Fact]
    public async Task OtherClientsAreListed()
    {
        _server.Register("aspire.api", "orders");
        _server.Register("aspire.billing.api", "orders");
        _server.Register("john");

        var others = await RavenDBCloudCertificates.GetOtherClientsAsync(Session(), Managed(), TestContext.Current.CancellationToken);

        Assert.Equal(["aspire.billing.api"], others);
    }

    [Fact]
    public async Task RevokingRetriesThenFailsTheDeployment()
    {
        _server.Register("aspire.api", "orders");
        var deployment = Managed();

        _server.FailingDeletes = 1;
        await _certificates.RevokeAllAsync(Session(), deployment, TestContext.Current.CancellationToken);
        Assert.Empty(_server.Certificates);

        _server.Register("aspire.api", "orders");
        _server.FailingDeletes = RavenDBCloudCertificates.RevokeAttempts;
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _certificates.RevokeAllAsync(Session(), deployment, TestContext.Current.CancellationToken));

        Assert.Contains("Could not revoke client certificate 'aspire.api'", exception.Message, StringComparison.Ordinal);
    }
}
