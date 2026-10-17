// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Foundry;
using Microsoft.Extensions.Configuration;

#pragma warning disable ASPIREMCP001
#pragma warning disable ASPIREAGENTS001

var builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<IResourceWithConnectionString> project;
IResourceBuilder<IResourceWithConnectionString> chat;

// Existing connections are the safe default: starting the playground must not
// silently create Azure resources or a billable model deployment.
if (builder.Configuration.GetValue("UseExistingFoundry", true))
{
    project = builder.AddConnectionString("agentsproject");
    chat = builder.AddConnectionString("chat");
}
else
{
    var foundry = builder.AddFoundry("agents-foundry");
    var foundryProject = foundry.AddProject("agentsproject");
    project = foundryProject;
    chat = foundryProject.AddModelDeployment("chat", FoundryModel.OpenAI.Gpt41Mini);
}

var a2aAgent = builder.AddUvicornApp("a2a-jsonrpc-agent", "../weather-agent-python", "weather_agent_python.main:app")
    .WithUv()
    .WithReference(project)
    .WithReference(chat)
    .WaitFor(chat)
    .AsAgent(AgentProtocol.A2A);

builder.AddProject<Projects.ResponsesAgent>("responses-agent")
    .WithHttpEndpoint(env: "PORT")
    .WithReference(chat)
    .WithEnvironment("A2A_JSONRPC_AGENT_AGENTCARD_URL",
        ReferenceExpression.Create($"{a2aAgent.GetEndpoint("http")}/.well-known/agent-card.json"))
    .WaitFor(chat)
    .AsAgent(AgentProtocol.Responses, agentName: "responses-agent")
    .WithMcpServer()
    .WithMcpToolCommands();

builder.AddNodeApp("vercel-ai-responses-agent", "../vercel-ai-responses-agent", "app.ts")
    .WithRunScript("start")
    .WithNpm(installCommand: "ci")
    .WithHttpEndpoint(env: "PORT")
    .WithReference(chat)
    .WaitFor(chat)
    .AsAgent(AgentProtocol.Responses, agentName: "vercel-ai-responses-agent");

builder.AddProject<Projects.McpAgent>("mcp-agent")
    .WithHttpEndpoint(env: "PORT")
    .WithReference(chat)
    .WaitFor(chat)
    .WithMcpServer()
    .WithMcpToolCommands();

builder.AddUvicornApp("agui-acp-agent", "../agui-acp-agent-python", "agui_acp_agent.main:app")
    .WithUv()
    .WithReference(project)
    .WithReference(chat)
    .WaitFor(chat)
    .AsAgent(AgentProtocol.AgUi)
    .AsAgent(AgentProtocol.Acp, agentName: "agui-acp-agent");

builder.AddProject<Projects.AgentEnvDump>("agent-env-dump")
    .WithEnvironment("A2A_JSONRPC_AGENT_AGENTCARD_URL",
        ReferenceExpression.Create($"{a2aAgent.GetEndpoint("http")}/.well-known/agent-card.json"))
    .WaitFor(a2aAgent);

builder.Build().Run();
