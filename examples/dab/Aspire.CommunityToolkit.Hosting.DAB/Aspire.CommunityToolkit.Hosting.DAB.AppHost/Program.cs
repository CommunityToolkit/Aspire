using Aspire.CommunityToolkit.Azure.Hosting.DataApiBuilder;

var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddConnectionString("sqldb");

var dab = builder.AddDataAPIBuilder("dab")
    .WithReference(sql);

builder.Build().Run();
