using CommunityToolkit.Aspire.Hosting.ActiveMQ.MassTransit;
using MassTransit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services
    .AddSingleton(KebabCaseEndpointNameFormatter.Instance)
    .AddSingleton<MessageCounter>();
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

app.MapPost("/send/{text}", async (string text,
        [FromServices] IRequestClient<Message> requestClient,
        [FromServices] ILogger<Program> logger) =>
    {
        logger.LogInformation("Send message: {Text}", text);
        Response<MessageReply> response = await requestClient.GetResponse<MessageReply>(new { Text = text });
        logger.LogInformation("Sent message: {Text}", text);
        return response.Message.Reply;
    })
    .WithName("SendMessage");

app.MapGet("/received", ([FromServices] MessageCounter messageCounter) => messageCounter)
    .WithName("ReceivedMessages");

app.MapDefaultEndpoints();
app.Run();
