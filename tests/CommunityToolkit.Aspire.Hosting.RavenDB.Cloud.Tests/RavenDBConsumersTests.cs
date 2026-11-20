using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Hosting.RavenDB;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.RavenDB.Cloud.Tests;

/// <summary>The applications that use a server, and the settings a deployment gives them (shared with other deployment packages).</summary>
public class RavenDBConsumersTests
{
    [Fact]
    public void ConsumersAreFoundWithTheirDatabasesAndConnectionNames()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var ravendb = builder.AddRavenDB("ravendb");
        var orders = ravendb.AddDatabase("orders");
        ravendb.AddDatabase("reports");
        var other = builder.AddRavenDB("other");

        builder.AddContainer("api", "busybox").WithReference(orders);
        builder.AddContainer("admin", "busybox").WithReference(ravendb).WithReference(other);
        builder.AddContainer("unrelated", "busybox").WithReference(other);

        var consumers = RavenDBConsumers.Find(new DistributedApplicationModel(builder.Resources), ravendb.Resource).ToDictionary(c => c.Resource.Name);

        Assert.Equal(["admin", "api"], consumers.Keys.Order());
        Assert.Equal(["orders"], consumers["api"].ConnectionNames);
        Assert.Equal(["orders"], consumers["api"].Databases);
        Assert.Equal(1, consumers["api"].ServersReferenced);

        // A reference to the server reaches every database declared on it.
        Assert.Equal(["ravendb"], consumers["admin"].ConnectionNames);
        Assert.Equal(["orders", "reports"], consumers["admin"].Databases);
        Assert.Equal(2, consumers["admin"].ServersReferenced);
    }

    /// <summary>A resource that is not published, such as a tool for local development, gets nothing and needs no target.</summary>
    [Fact]
    public void ResourceExcludedFromPublishIsNoConsumer()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var ravendb = builder.AddRavenDB("ravendb");
        var orders = ravendb.AddDatabase("orders");

        builder.AddContainer("api", "busybox").WithReference(orders);
        builder.AddContainer("seed", "busybox").WithReference(orders).ExcludeFromManifest();

        var consumers = RavenDBConsumers.Find(new DistributedApplicationModel(builder.Resources), ravendb.Resource);

        Assert.Equal("api", Assert.Single(consumers).Resource.Name);
    }

    [Fact]
    public async Task CertificateGoesUnderEachConnectionNameAndTheDefaultSection()
    {
        var api = new ContainerResource("api");

        RavenDBClientConfiguration.DeliverCertificate(new RavenDBConsumer(api, ["orders"], ["orders"], 1), "certificate-content");

        var variables = await api.GetEnvironmentVariablesAsync();
        Assert.Equal("certificate-content", variables["Aspire__RavenDB__Client__orders__CertificateBase64"]);
        Assert.Equal("certificate-content", variables["Aspire__RavenDB__Client__CertificateBase64"]);
    }

    [Fact]
    public async Task WithSeveralServersTheDefaultSectionStaysEmpty()
    {
        var api = new ContainerResource("api");

        RavenDBClientConfiguration.DeliverCertificate(new RavenDBConsumer(api, ["orders"], ["orders"], 2), "certificate-content");

        var variables = await api.GetEnvironmentVariablesAsync();
        Assert.Equal("certificate-content", variables["Aspire__RavenDB__Client__orders__CertificateBase64"]);
        Assert.False(variables.ContainsKey("Aspire__RavenDB__Client__CertificateBase64"));
    }
}
