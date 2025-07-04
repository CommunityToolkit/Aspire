var builder = DistributedApplication.CreateBuilder(args);

var server = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_McpInspector_McpServer>("mcp-server");

builder.AddMcpInspector("mcp-inspector")
    .WithMcpServer(server)
    ;

builder.Build().Run();
