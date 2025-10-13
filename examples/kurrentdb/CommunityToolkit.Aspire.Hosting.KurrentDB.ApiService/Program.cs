using CommunityToolkit.Aspire.Hosting.KurrentDB.ApiService;
using EventStore.Client;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddKurrentDBClient("kurrentdb");

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/account/create", async (EventStoreClient eventStore, CancellationToken cancellationToken) =>
{
    var account = Account.Create(Guid.NewGuid(), "John Doe");

    account.Deposit(100);

    await eventStore.AppendAcountEvents(account, cancellationToken);

    return Results.Created($"/account/{account.Id}", account);
});

app.MapGet("/account/{id:guid}", async (Guid id, EventStoreClient eventStore, CancellationToken cancellationToken) =>
{
    var account = await eventStore.GetAccount(id, cancellationToken);
    if (account is null)
    {
        return Results.NotFound();
    }

    return TypedResults.Ok(account);
});

app.MapPost("/account/{id:guid}/deposit", async (Guid id, DepositRequest request, EventStoreClient eventStore, CancellationToken cancellationToken) =>
{
    var account = await eventStore.GetAccount(id, cancellationToken);
    if (account is null)
    {
        return Results.NotFound();
    }

    account.Deposit(request.Amount);

    await eventStore.AppendAcountEvents(account, cancellationToken);

    return Results.Ok();
});

app.MapPost("/account/{id:guid}/withdraw", async (Guid id, WithdrawRequest request, EventStoreClient eventStore, CancellationToken cancellationToken) =>
{
    var account = await eventStore.GetAccount(id, cancellationToken);
    if (account is null)
    {
        return Results.NotFound();
    }

    account.Withdraw(request.Amount);

    await eventStore.AppendAcountEvents(account, cancellationToken);

    return Results.Ok();
});

app.Run();

public record DepositRequest(decimal Amount);
public record WithdrawRequest(decimal Amount);
