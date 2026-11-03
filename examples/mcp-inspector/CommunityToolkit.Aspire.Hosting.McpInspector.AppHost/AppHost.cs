var builder = DistributedApplication.CreateBuilder(args);

#pragma warning disable ASPIREDOTNETPROJECT001
var server = builder.AddDotnetProject("mcp-server", new Projects.CommunityToolkit_Aspire_Hosting_McpInspector_McpServer().ProjectPath);
#pragma warning restore ASPIREDOTNETPROJECT001

builder.AddMcpInspector("mcp-inspector")
    .WithMcpServer(server)
    ;

builder.Build().Run();
