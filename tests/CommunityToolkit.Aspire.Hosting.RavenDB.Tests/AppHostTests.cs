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
        using var cancellationToken = new CancellationTokenSource();
        cancellationToken.CancelAfter(TimeSpan.FromMinutes(5));

        var connectionName = "ravendb";
        var databaseName = "ravenDatabase";

        await fixture.ResourceNotificationService.WaitForResourceAsync(connectionName, KnownResourceStates.Running, cancellationToken.Token).WaitAsync(TimeSpan.FromMinutes(5), cancellationToken.Token);

        var endpoint = fixture.GetEndpoint(connectionName, "http");
        Assert.NotNull(endpoint);
        Assert.False(string.IsNullOrWhiteSpace(endpoint.OriginalString));
        Assert.True(endpoint.Scheme == Uri.UriSchemeHttp);

        var appModel = fixture.App.Services.GetRequiredService<DistributedApplicationModel>();
        var serverResource = Assert.Single(appModel.Resources.OfType<RavenDBServerResource>());
        var dbResource = Assert.Single(appModel.Resources.OfType<RavenDBDatabaseResource>());

        var serverConnectionString = await serverResource.ConnectionStringExpression.GetValueAsync(cancellationToken.Token);
        Assert.False(string.IsNullOrWhiteSpace(serverConnectionString));
        Assert.Contains(endpoint.OriginalString, serverConnectionString);
        Assert.Equal(databaseName, dbResource.DatabaseName);

        var databaseConnectionString = await dbResource.ConnectionStringExpression.GetValueAsync(cancellationToken.Token);
        Assert.False(string.IsNullOrWhiteSpace(databaseConnectionString));
        Assert.Equal($"URL={endpoint.OriginalString};Database={databaseName}", databaseConnectionString);
        Assert.Equal(databaseName, dbResource.DatabaseName);

        // Create RavenDB Client

        var clientBuilder = Host.CreateApplicationBuilder();
        clientBuilder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>($"ConnectionStrings:{connectionName}", databaseConnectionString)
        ]);

        clientBuilder.AddRavenDBClient(connectionName: connectionName);
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
