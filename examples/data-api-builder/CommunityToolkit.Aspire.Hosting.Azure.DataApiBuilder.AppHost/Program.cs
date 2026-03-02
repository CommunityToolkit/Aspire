var builder = DistributedApplication.CreateBuilder(args);

var sqlScript = File.ReadAllText("./sql-server.sql");

var sqlDatabase = builder
    .AddSqlServer("sql")
    .WithDataVolume("trek-sql-data")
    .AddDatabase("trek")
    .WithCreationScript(sqlScript);

var dabConfig = new FileInfo("./dab-config.json");

var dab = builder.AddDataAPIBuilder("dab")
    .WithImageTag("1.7.86-rc")
    .WithConfigFile(dabConfig)
    .WaitFor(sqlDatabase)
    .WithReference(sqlDatabase);

var mcp = builder
    .AddMcpInspector("mcp-inspector", options =>
    {
        options.InspectorVersion = "0.20.0";
    })
    .WithMcpServer(dab, transportType: McpTransportType.StreamableHttp)
    .WithParentRelationship(dab)
    .WithEnvironment("DANGEROUSLY_OMIT_AUTH", "true")
    .WaitFor(dab);

builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Azure_DataApiBuilder_BlazorApp>("blazorApp")
    .WithReference(dab)
    .WaitFor(dab);

builder.Build().Run();
