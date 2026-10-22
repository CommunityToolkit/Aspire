using Raven.Client.Documents.Conventions;
using Raven.Client.Http;
using Raven.Client.ServerWide.Operations;
using Sparrow.Json;

namespace CommunityToolkit.Aspire.RavenDB.Client;

/// <summary>
/// Whether a database exists, asked about that one database. Every certificate may ask, also one limited to its own
/// databases, which cannot read database records; the answer needs no paging through the server's databases; and a
/// sharded database is found by its own name, where the list of names holds its shards.
/// </summary>
/// <param name="database">The name of the database.</param>
internal sealed class DatabaseExistsOperation(string database) : IServerOperation<bool>
{
    /// <inheritdoc />
    public RavenCommand<bool> GetCommand(DocumentConventions conventions, JsonOperationContext context) => new DatabaseExistsCommand(database);

    private sealed class DatabaseExistsCommand(string database) : RavenCommand<bool>
    {
        public override bool IsReadRequest => true;

        public override HttpRequestMessage CreateRequest(JsonOperationContext ctx, ServerNode node, out string url)
        {
            url = $"{node.Url}/databases?name={Uri.EscapeDataString(database)}&namesOnly=true";

            return new HttpRequestMessage { Method = HttpMethod.Get };
        }

        // The server answers 404 when there is no such database, which the request executor passes on as no response.
        public override void SetResponse(JsonOperationContext context, BlittableJsonReaderObject response, bool fromCache) =>
            Result = response is not null;
    }
}
