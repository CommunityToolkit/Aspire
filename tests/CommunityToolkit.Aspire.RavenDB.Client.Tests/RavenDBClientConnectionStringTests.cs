using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Raven.Client.Documents;

namespace CommunityToolkit.Aspire.RavenDB.Client.Tests;

public class RavenDBClientConnectionStringTests
{
    /// <summary>The URL lists every node of a cluster, as the RavenDB Cloud hosting integration writes it.</summary>
    [Fact]
    public void UrlListsTheNodesOfACluster()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(
        [
            new("ConnectionStrings:db", "URL=http://a.example:8080, http://b.example:8080,http://c.example:8080;Database=orders"),
            new("Aspire:RavenDB:Client:DisableHealthChecks", "true"),
        ]);

        builder.AddRavenDBClient("db");

        using var host = builder.Build();
        var store = host.Services.GetRequiredService<IDocumentStore>();

        Assert.Equal(["http://a.example:8080", "http://b.example:8080", "http://c.example:8080"], store.Urls);
        Assert.Equal("orders", store.Database);
    }
}
