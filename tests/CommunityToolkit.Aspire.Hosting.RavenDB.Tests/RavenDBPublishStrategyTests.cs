#pragma warning disable CTASPIRERAVENDB001 // The API for deployment packages, under test

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Hosting.RavenDB;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.RavenDB.Tests;

public class RavenDBPublishStrategyTests
{
    private static RavenDBPublishStrategy Strategy(string name = "PublishAsSomewhere") =>
        new(name, ReferenceExpression.Create($"https://a.example.ravendb.cloud"));

    [Fact]
    public void SecondStrategyIsAnErrorNamingBoth()
    {
        var server = new RavenDBServerResource("ravendb", isSecured: false);
        server.UsePublishStrategy(Strategy("PublishAsFirst"));

        var exception = Assert.Throws<DistributedApplicationException>(() => server.UsePublishStrategy(Strategy("PublishAsSecond")));

        Assert.Equal(
            "RavenDB server 'ravendb' is already published with PublishAsFirst, so it cannot also be published with PublishAsSecond. " +
            "A server has one publish strategy; to use a different one per environment, choose it with an if in the AppHost.",
            exception.Message);
    }

    [Fact]
    public async Task ConnectionStringPointsAtTheStrategysUrl()
    {
        var server = new RavenDBServerResource("ravendb", isSecured: true);
        var database = new RavenDBDatabaseResource("orders", "orders", server);

        server.UsePublishStrategy(Strategy());

        Assert.Equal("URL=https://a.example.ravendb.cloud", await server.ConnectionStringExpression.GetValueAsync(TestContext.Current.CancellationToken));
        Assert.Equal("URL=https://a.example.ravendb.cloud;Database=orders", await database.ConnectionStringExpression.GetValueAsync(TestContext.Current.CancellationToken));
        Assert.Equal("https://a.example.ravendb.cloud", await server.UriExpression.GetValueAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void EnsureCreatedDatabasesAreRemembered()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var ravendb = builder.AddRavenDB("ravendb");

        ravendb.AddDatabase("orders", ensureCreated: true);
        ravendb.AddDatabase("reports");

        Assert.Equal(["orders"], ravendb.Resource.DatabasesToCreate);
    }

    [Fact]
    public async Task PublishedElsewhereTheServerIsNotWaitedForNorPublished()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var ravendb = builder.AddRavenDB("ravendb");
        var orders = ravendb.AddDatabase("orders");
        var other = builder.AddContainer("other", "busybox");
        var api = builder.AddContainer("api", "busybox").WithReference(orders).WaitFor(ravendb).WaitFor(orders).WaitFor(other);

        ravendb.WithPublishStrategy(Strategy());

        await using var app = await builder.BuildAsync(TestContext.Current.CancellationToken);
        await builder.Eventing.PublishAsync(
            new BeforeStartEvent(app.Services, app.Services.GetRequiredService<DistributedApplicationModel>()),
            TestContext.Current.CancellationToken);

        var waits = api.Resource.Annotations.OfType<WaitAnnotation>().Select(w => w.Resource.Name).ToList();
        Assert.Equal(["other"], waits);
        Assert.Contains(ravendb.Resource.Annotations, a => a is ManifestPublishingCallbackAnnotation { Callback: null });
    }
}
