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
    public void ConsumersAreFoundWithTheirDatabases()
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
        Assert.Equal(["orders"], consumers["api"].Databases);
        Assert.Equal(1, consumers["api"].ServersReferenced);

        // A reference to the server reaches every database declared on it.
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
        using var builder = TestDistributedApplicationBuilder.Create();
        var ravendb = builder.AddRavenDB("ravendb");
        var api = builder.AddContainer("api", "busybox").WithReference(ravendb.AddDatabase("orders"));

        Deliver(builder, ravendb.Resource);

        var variables = await api.Resource.GetEnvironmentVariablesAsync(DistributedApplicationOperation.Publish);
        Assert.Equal("certificate-content", variables["Aspire__RavenDB__Client__orders__CertificateBase64"]);
        Assert.Equal("certificate-content", variables["Aspire__RavenDB__Client__CertificateBase64"]);
    }

    /// <summary>
    /// With two servers there is no default section to fall back on: a connection renamed with
    /// <c>WithReference(db, connectionName: ...)</c> gets the certificate under its new name.
    /// </summary>
    [Fact]
    public async Task WithSeveralServersTheCertificateGoesUnderTheConnectionNamesOnly()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var ravendb = builder.AddRavenDB("ravendb");
        var other = builder.AddRavenDB("other");
        var api = builder.AddContainer("api", "busybox")
            .WithReference(ravendb.AddDatabase("orders"), connectionName: "primary")
            .WithReference(other);

        Deliver(builder, ravendb.Resource);

        var variables = await api.Resource.GetEnvironmentVariablesAsync(DistributedApplicationOperation.Publish);
        Assert.Equal("certificate-content", variables["Aspire__RavenDB__Client__primary__CertificateBase64"]);
        Assert.False(variables.ContainsKey("Aspire__RavenDB__Client__orders__CertificateBase64"));
        Assert.False(variables.ContainsKey("Aspire__RavenDB__Client__other__CertificateBase64"));
        Assert.False(variables.ContainsKey("Aspire__RavenDB__Client__CertificateBase64"));
    }

    private static void Deliver(IDistributedApplicationTestingBuilder builder, RavenDBServerResource server)
    {
        foreach (var consumer in RavenDBConsumers.Find(new DistributedApplicationModel(builder.Resources), server))
        {
            RavenDBClientConfiguration.DeliverCertificate(consumer, server, "certificate-content");
        }
    }
}
