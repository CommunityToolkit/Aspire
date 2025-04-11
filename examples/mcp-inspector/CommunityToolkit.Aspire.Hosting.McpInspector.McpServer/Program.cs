using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddMcpServer()
    .WithTools<McpServerTools>();

var app = builder.Build();

app.MapMcp();

app.Run();

[McpServerToolType]
class McpServerTools
{
    [McpServerTool, Description("An echo tool")]
    public static string Echo(string message) => $"Echo: {message}";
}