using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.RavenDB.Client;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Raven.Client.Documents;

namespace CommunityToolkit.Aspire.Hosting.RavenDB.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.RavenDB_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.RavenDB_AppHost>>
{
    [Fact]
    public async Task TestAppHost()
    {
        using var cancellationToken = new CancellationTokenSource();
        cancellationToken.CancelAfter(TimeSpan.FromMinutes(5));

        var resourceName = "ravenServer";
        await fixture.ResourceNotificationService.WaitForResourceAsync(resourceName, KnownResourceStates.Running, cancellationToken.Token).WaitAsync(TimeSpan.FromMinutes(5), cancellationToken.Token);

        var connectionString = fixture.GetEndpoint(resourceName, "http");
        Assert.NotNull(connectionString);

        var appModel = fixture.App.Services.GetRequiredService<DistributedApplicationModel>();
        var serverResource = Assert.Single(appModel.Resources.OfType<RavenDBServerResource>());
        var dbResource = Assert.Single(appModel.Resources.OfType<RavenDBDatabaseResource>());

        var url = await serverResource.ConnectionStringExpression.GetValueAsync(cancellationToken.Token);
        Assert.NotNull(url);
        Assert.Equal(connectionString.OriginalString, url);
        Assert.Equal("TestDatabase", dbResource.DatabaseName);

        await Task.Delay(10000, cancellationToken.Token);

        // Create RavenDB Client

        var clientBuilder = Host.CreateApplicationBuilder();
        clientBuilder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>($"ConnectionStrings:{serverResource.Name}", url)
        ]);

        clientBuilder.AddRavenDBClient(new RavenDBClientSettings(urls: new[] { url }, databaseName: "TestDatabase") { CreateDatabase = true });
        var host = clientBuilder.Build();

        using var documentStore = host.Services.GetRequiredService<IDocumentStore>();

        using (var session = documentStore.OpenAsyncSession())
        {
            await session.StoreAsync(new { Id = "Test/1", Name = "Test Document" }, cancellationToken.Token);
            await session.SaveChangesAsync(cancellationToken.Token);
        }

        using (var session = documentStore.OpenAsyncSession())
        {
            var doc = await session.LoadAsync<dynamic>("Test/1", cancellationToken.Token);
            Assert.NotNull(doc);
            Assert.Equal("Test Document", doc.Name.ToString());
        }
    }
}
