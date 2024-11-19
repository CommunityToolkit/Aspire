using CommunityToolkit.Aspire.Hosting.ActiveMQ.MassTransit;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services
    .TryAddSingleton(KebabCaseEndpointNameFormatter.Instance);
builder.Services.AddMassTransit(x =>
{
    x.UsingActiveMq((context, cfg) =>
    {
        string connectionString = builder.Configuration.GetConnectionString("amq")!;
        cfg.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter("aspire", false));
        cfg.Host(new Uri(connectionString), _ => {});
    });
    x.AddConsumers(typeof(HelloWorldConsumer).Assembly);
});

WebApplication app = builder.Build();

app.UseHttpsRedirection();

app.MapPost("/send/{text}", async (string text,
        [FromServices] IPublishEndpoint publishEndpoint,
        [FromServices] ILogger<Program> logger) =>
    {
        logger.LogInformation("Send message: {Text}", text);
        await publishEndpoint.Publish<Message>(new { Text = text });
        logger.LogInformation("Sent message: {Text}", text);

    })
    .WithName("SendMessage");

app.Run();
