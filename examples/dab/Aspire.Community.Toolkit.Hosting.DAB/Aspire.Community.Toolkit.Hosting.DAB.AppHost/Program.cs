using Aspire.CommunityToolkit.Azure.Hosting.DataApiBuilder;

var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddConnectionString("sqldb");

var containerapp = builder.AddDataAPIBuilderApp("containerapp")
    .WithReference(sql);

builder.Build().Run();
