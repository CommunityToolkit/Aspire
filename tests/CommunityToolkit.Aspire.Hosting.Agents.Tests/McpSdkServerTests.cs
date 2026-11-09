// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable EXTEXP0001

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Aspire.Hosting.Agents.Tests.TestServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace CommunityToolkit.Aspire.Hosting.Agents.Tests;

public partial class WithMcpToolCommandsTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task WithMcpToolCommands_OfficialSdkServerSmoke(bool stateless, bool fail)
    {
        var serverBuilder = WebApplication.CreateBuilder();
        serverBuilder.Logging.ClearProviders();
        serverBuilder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        serverBuilder.Services.AddMcpServer()
            .WithHttpTransport(options =>
            {
                options.Stateless = stateless;
            })
            .WithTools<LocalTools>();
        await using var server = serverBuilder.Build();
        var terminatedSessions = new ConcurrentQueue<string>();
        var toolSessions = new ConcurrentQueue<string>();
        server.Use(async (context, next) =>
        {
            if (context.Request.Headers["X-Test-Authorization"] != "local-smoke-test")
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            if (HttpMethods.IsDelete(context.Request.Method))
            {
                terminatedSessions.Enqueue(context.Request.Headers["Mcp-Session-Id"].ToString());
            }
            else if (HttpMethods.IsPost(context.Request.Method))
            {
                context.Request.EnableBuffering();
                using var payload = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
                context.Request.Body.Position = 0;
                if (payload.RootElement.TryGetProperty("method", out var method) && method.GetString() == "tools/call")
                {
                    toolSessions.Enqueue(context.Request.Headers["Mcp-Session-Id"].ToString());
                }
            }

            await next(context);
        });
        server.MapMcp("/mcp");
        await server.StartAsync(TestContext.Current.CancellationToken);
        var address = Assert.Single(server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses);

        using var builder = TestDistributedApplicationBuilder.Create(options => options.TrustDeveloperCertificate = false);
        builder.Services.AddHttpClient(string.Empty, client =>
            client.DefaultRequestHeaders.Add("X-Test-Authorization", "local-smoke-test")).RemoveAllResilienceHandlers();
        var resource = builder.AddResource(new McpTestResource("app"))
            .WithAnnotation(new McpServerEndpointAnnotation((_, _) => Task.FromResult<Uri?>(new Uri($"{address}/mcp"))))
            .WithMcpToolCommands();
        using var app = builder.Build();
        await app.StartAsync().DefaultTimeout();
        await MoveResourceToRunningStateAsync(app, resource.Resource);

        var result = await app.ResourceCommands.ExecuteCommandAsync(
            resource.Resource, "app-mcp-call-tool",
            CreateMcpArguments(fail ? "fail" : "echo", fail ? "{}" : """{"value":"local SDK smoke"}""")).DefaultTimeout();

        Assert.Equal(!fail, result.Success);
        Assert.Equal(CommandResultFormat.Json, result.Data?.Format);
        var envelope = JsonNode.Parse(result.Data!.Value)!;
        if (fail)
        {
            Assert.Equal("MCP tool reported an error.", result.Message);
            Assert.True(envelope["isError"]!.GetValue<bool>());
            Assert.Contains("Local diagnostic failure.", envelope["content"]![0]!["text"]!.GetValue<string>());
            await using var logs = app.Services.GetRequiredService<ResourceLoggerService>()
                .GetAllAsync(resource.Resource).GetAsyncEnumerator();
            Assert.True(await logs.MoveNextAsync().AsTask().DefaultTimeout());
            Assert.Contains(logs.Current, line => line.Content.Contains("MCP tool reported an error.", StringComparison.Ordinal));
            Assert.DoesNotContain(logs.Current, line => line.Content.Contains("Local diagnostic failure.", StringComparison.Ordinal));
        }
        else
        {
            Assert.Equal("2.0", envelope["jsonrpc"]!.GetValue<string>());
            Assert.NotNull(envelope["id"]);
            Assert.Equal("local SDK smoke", envelope["result"]!["content"]![0]!["text"]!.GetValue<string>());
        }
        // Aspire's independent metadata discovery may also open sessions. Verify
        // cleanup of the session that actually executed our command, not probe traffic.
        var toolSession = Assert.Single(toolSessions);
        Assert.Equal(stateless ? 0 : 1, terminatedSessions.Count(id => id == toolSession));
        Assert.Equal(stateless, string.IsNullOrEmpty(toolSession));
        Assert.All(terminatedSessions, id => Assert.NotEmpty(id));
        await server.StopAsync(TestContext.Current.CancellationToken);
    }

    [McpServerToolType]
    public sealed class LocalTools
    {
        [McpServerTool(Name = "echo")]
        public static string Echo(string value) => value;

        [McpServerTool(Name = "fail")]
        public static string Fail() => throw new McpException("Local diagnostic failure.");
    }
}
