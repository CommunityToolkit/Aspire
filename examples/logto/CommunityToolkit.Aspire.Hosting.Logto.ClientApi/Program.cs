using CommunityToolkit.Aspire.Hosting.Logto.Client;

var builder = WebApplication.CreateBuilder(args);

builder.AddLogtoSDKClient("logto");

var app = builder.Build();



app.UseHttpsRedirection();


app.Run();


app.MapGet("/", () => "Hello World!");