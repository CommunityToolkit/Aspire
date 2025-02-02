using Raven.Client.Documents;
using Raven.Embedded;

namespace CommunityToolkit.Aspire.RavenDB.Client.Tests;

public sealed class RavenDbServerFixture : IAsyncLifetime, IDisposable
{
    public EmbeddedServer Server { get; } = EmbeddedServer.Instance;
    public IDocumentStore? Store { get; private set; }
    public string? ConnectionString { get; private set; }

    public async Task InitializeAsync()
    {
        Server.StartServer();

        var uri = await Server.GetServerUriAsync();
        var serverUrl = uri.OriginalString;
        ConnectionString = serverUrl;

        Store = new DocumentStore { Urls = new[] { serverUrl } };
    }

    public void CreateDatabase(string databaseName) =>
        CreateDatabase(new DatabaseOptions(databaseName));

    public void CreateDatabase(DatabaseOptions options)
    {
        Store = Server.GetDocumentStore(options);
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Store?.Dispose();
        Server.Dispose();
    }
}
