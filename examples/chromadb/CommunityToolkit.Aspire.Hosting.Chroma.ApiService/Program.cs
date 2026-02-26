using ChromaDB.Client;
using ChromaDB.Client.Models;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddChromaClient("chroma");

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapPost("/create", async (ChromaClient chroma, IConfiguration config, IHttpClientFactory factory) =>
{
    var collectionName = $"movies_{Guid.NewGuid():N}";
    var collection = await chroma.CreateCollection(collectionName);
    
    var endpoint = GetChromaEndpoint(config, "chroma");
    var httpClient = factory.CreateClient("chroma");
    var options = new ChromaConfigurationOptions(endpoint);
    var collectionClient = new ChromaCollectionClient(collection, options, httpClient);

    await collectionClient.Add(
        ids: ["1", "2"],
        embeddings: [new ReadOnlyMemory<float>([0.1f, 0.2f, 0.3f]), new ReadOnlyMemory<float>([0.4f, 0.5f, 0.6f])],
        metadatas: [
            new Dictionary<string, object> { { "title", "Inception" } },
            new Dictionary<string, object> { { "title", "Interstellar" } }
        ],
        documents: ["A thief who enters the dreams of others.", "A group of explorers travel through a wormhole."]
    );

    return Results.Ok(new { Collection = collectionName, Count = 2 });
});

app.MapGet("/query", async (ChromaClient chroma, IConfiguration config, IHttpClientFactory factory, string collectionName) =>
{
    var collection = await chroma.GetCollection(collectionName);
    var endpoint = GetChromaEndpoint(config, "chroma");
    var httpClient = factory.CreateClient("chroma");
    var options = new ChromaConfigurationOptions(endpoint);
    var collectionClient = new ChromaCollectionClient(collection, options, httpClient);

    var results = await collectionClient.Query(
        queryEmbeddings: new ReadOnlyMemory<float>([0.1f, 0.2f, 0.3f]),
        nResults: 1
    );

    return Results.Ok(results);
});

static string GetChromaEndpoint(IConfiguration config, string connectionName)
{
    var connectionString = config.GetConnectionString(connectionName);
    if (!string.IsNullOrEmpty(connectionString))
    {
        if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
        {
            return uri.ToString();
        }
        
        // Handle "Endpoint=..." format
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.StartsWith("Endpoint=", StringComparison.OrdinalIgnoreCase))
            {
                return part.Substring("Endpoint=".Length);
            }
        }
    }
    
    return config["Aspire:Chroma:Endpoint"] ?? throw new InvalidOperationException("ChromaDB endpoint not found.");
}

app.Run();
