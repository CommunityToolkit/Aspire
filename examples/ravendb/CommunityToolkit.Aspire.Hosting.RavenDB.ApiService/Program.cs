using Raven.Client.Documents;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRavenDBClient(connectionName: "ravendb", configureSettings: settings =>
{
    settings.CreateDatabase = true;
    settings.DatabaseName = "ravenDatabase";
});

var app = builder.Build();

app.MapGet("/create", async (IDocumentStore documentStore) =>
{
    using var session = documentStore.OpenAsyncSession();
    var company = new Company
    {
        Name = "RavenDB",
        Phone = "(26) 642-7012",
        Fax = "(26) 642-7012"
    };

    await session.StoreAsync(company, "companies/ravendb");
    await session.SaveChangesAsync();
});

app.MapGet("/get", async (IDocumentStore documentStore) =>
{
    using var session = documentStore.OpenAsyncSession();
    var company = await session.LoadAsync<Company>("companies/ravendb");
    return company;
});

app.MapDefaultEndpoints();

app.Run();


public class Company
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
}
