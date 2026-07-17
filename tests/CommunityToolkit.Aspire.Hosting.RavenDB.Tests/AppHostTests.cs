using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Raven.Client.Documents;

namespace CommunityToolkit.Aspire.Hosting.RavenDB.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_RavenDB_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_RavenDB_AppHost>>
{
    [Fact]
    public async Task TestAppHost()
    {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMinutes(5));

        var connectionName = "ravendb";
        var databaseName = "ravenDatabase";

        await fixture.ResourceNotificationService.WaitForResourceAsync(connectionName, KnownResourceStates.Running, cts.Token).WaitAsync(TimeSpan.FromMinutes(5), cts.Token);

        var endpoint = fixture.GetEndpoint(connectionName, "http");
        Assert.NotNull(endpoint);
        Assert.False(string.IsNullOrWhiteSpace(endpoint.OriginalString));
        Assert.True(endpoint.Scheme == Uri.UriSchemeHttp);

        var appModel = fixture.App.Services.GetRequiredService<DistributedApplicationModel>();
        var serverResource = Assert.Single(appModel.Resources.OfType<RavenDBServerResource>());
        var dbResource = Assert.Single(appModel.Resources.OfType<RavenDBDatabaseResource>());

        var serverConnectionString = await serverResource.ConnectionStringExpression.GetValueAsync(cts.Token);
        Assert.False(string.IsNullOrWhiteSpace(serverConnectionString));
        Assert.Contains(endpoint.OriginalString, serverConnectionString);
        Assert.Equal(databaseName, dbResource.DatabaseName);

        var databaseConnectionString = await dbResource.ConnectionStringExpression.GetValueAsync(cts.Token);
        Assert.False(string.IsNullOrWhiteSpace(databaseConnectionString));
        Assert.Equal($"URL={endpoint.OriginalString};Database={databaseName}", databaseConnectionString);
        Assert.Equal(databaseName, dbResource.DatabaseName);

        // Create RavenDB Client

        var clientBuilder = Host.CreateApplicationBuilder();
        clientBuilder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>($"ConnectionStrings:{connectionName}", databaseConnectionString)
        ]);

        clientBuilder.AddRavenDBClient(connectionName: connectionName, configureSettings: settings =>
        {
            settings.CreateDatabase = true;
        });
        var host = clientBuilder.Build();

        using var documentStore = host.Services.GetRequiredService<IDocumentStore>();

        using (var session = documentStore.OpenAsyncSession())
        {
            await session.StoreAsync(new { Id = "Test/1", Name = "Test Document" }, cts.Token);
            await session.SaveChangesAsync(cts.Token);
        }

        using (var session = documentStore.OpenAsyncSession())
        {
            var doc = await session.LoadAsync<dynamic>("Test/1", cts.Token);
            Assert.NotNull(doc);
            Assert.Equal("Test Document", doc.Name.ToString());
        }
    }

    [Fact]
    public async Task DatabaseResourceHasStudioUrl()
    {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMinutes(5));

        var serverName = "ravendb";
        var databaseResourceName = "ravenDatabase";

        // The database becomes healthy only after the server's endpoints are allocated, which is when
        // the "RavenDB Studio" URL annotation is added to the database resource.
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(databaseResourceName)
            .WaitAsync(TimeSpan.FromMinutes(5), cts.Token);

        var appModel = fixture.App.Services.GetRequiredService<DistributedApplicationModel>();
        var dbResource = Assert.Single(appModel.Resources.OfType<RavenDBDatabaseResource>());

        var serverEndpoint = fixture.GetEndpoint(serverName, "http"); // e.g. http://localhost:9534
        Assert.NotNull(serverEndpoint);

        // Deep-link uses the physical database name (URL-escaped), not the resource name, so assert against
        // the actual DatabaseName to stay correct even when AddDatabase(name, databaseName: ...) differ.
        var expectedUrl =
            $"{serverEndpoint.OriginalString.TrimEnd('/')}/studio/index.html#databases/documents?&database={Uri.EscapeDataString(dbResource.DatabaseName)}";

        var studioUrl = Assert.Single(dbResource.Annotations.OfType<ResourceUrlAnnotation>(),
            u => u.DisplayText == "RavenDB Studio");
        Assert.Equal(expectedUrl, studioUrl.Url);

        // The dashboard renders links from the resource snapshot (published via PublishUpdateAsync), not the
        // annotation, so assert the URL actually reached the snapshot — otherwise the link could be absent
        // from the dashboard even though the annotation is present.
        Assert.True(fixture.ResourceNotificationService.TryGetCurrentState(databaseResourceName, out var resourceEvent));
        var snapshotUrl = Assert.Single(resourceEvent!.Snapshot.Urls,
            u => u.DisplayProperties.DisplayName == "RavenDB Studio");
        Assert.Equal(expectedUrl, snapshotUrl.Url);
    }
}
