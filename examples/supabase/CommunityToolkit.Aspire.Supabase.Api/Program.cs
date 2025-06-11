//Using
// - https://github.com/supabase-community/supabase-csharp/blob/master/Examples/SupabaseExample/Program.cs
// - https://github.com/supabase-community/supabase-csharp/blob/master/Examples/BlazorWebAssemblySupabaseTemplate/Program.cs
// as the example to setup supabase

using CommunityToolkit.Aspire.Supabase.Api.Models;
using Supabase.Postgrest.Responses;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);


builder.Services.AddScoped<Supabase.Client>(
    provider => new Supabase.Client(
        "url",
        "key",
        new Supabase.SupabaseOptions
        {
            AutoConnectRealtime = true,
        }
    )
);


WebApplication app = builder.Build();

//Create a CRUD for Movie using supabase client
app.MapGet("/movies", async (Supabase.Client client) =>
{
    ModeledResponse<Movie> response = await client.From<Movie>().Get();
    return response.Models;
});

app.MapGet("/movies/{id:int}", async (Supabase.Client client, int id) =>
{
    ModeledResponse<Movie> response = await client.From<Movie>().Where(x => x.Id == id).Get();
    return response.Model;
});

app.MapPost("/movies", async (Supabase.Client client, Movie movie) =>
{
    ModeledResponse<Movie> response = await client.From<Movie>().Insert(movie);
    return response.Model;
});

app.MapPut("/movies/{id:int}", async (Supabase.Client client, int id, Movie movie) =>
{
    movie.Id = id; // Ensure the ID is set for the update
    ModeledResponse<Movie> response = await client.From<Movie>().Where(x => x.Id == id).Update(movie);
    return response.Model;
});

app.MapDelete("/movies/{id:int}", async (Supabase.Client client, int id) =>
{
    await client.From<Movie>().Where(x => x.Id == id).Delete();
    return TypedResults.Ok();
});

app.MapGet("/", () => "Hello World!");

app.Run();