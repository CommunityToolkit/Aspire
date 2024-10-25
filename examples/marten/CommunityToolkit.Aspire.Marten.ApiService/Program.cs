using CommunityToolkit.Aspire.Marten.ApiService;
using Marten;
using Marten.Services;

#pragma warning disable CTASPIREMARTEN001

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddMartenClient("postgres", configureStoreOptions: (storeOptions) =>
{
    storeOptions.OpenTelemetry.TrackConnections = TrackLevel.Verbose;
   
    storeOptions.OpenTelemetry.TrackEventCounters();
});

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/create", async (DocumentStore documentStore) =>
{
    using var session = documentStore.LightweightSession();

    var documents = new Movie[] {
                new() { Id = "1", Title = "Carol", Genres = ["Romance", "Drama"]  },
                new() { Id = "2", Title = "Wonder Woman", Genres = ["Action", "Adventure"] },
                new() { Id = "3", Title = "Life of Pi", Genres = ["Adventure", "Drama"] },
                new() { Id = "4", Title = "Mad Max: Fury Road", Genres = ["Adventure", "Science Fiction"] },
                new() { Id = "5", Title = "Moana", Genres = ["Fantasy", "Action"] },
                new() { Id = "6", Title = "Philadelphia", Genres = ["Drama"] }
            };

    session.Store<Movie>(documents);
    await session.SaveChangesAsync();
});

app.MapGet("/get", async (DocumentStore documentStore) =>
{
    using var session = documentStore.QuerySession();

    var data = await session.Query<Movie>() 
        .ToListAsync();

    return data;
});

app.Run();
