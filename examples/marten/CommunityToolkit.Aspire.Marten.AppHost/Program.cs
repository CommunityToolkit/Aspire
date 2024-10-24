var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("pg");
var db = postgres.AddDatabase("postgres");

builder.AddProject<Projects.CommunityToolkit_Aspire_Marten_ApiService>("communitytoolkit-aspire-marten-apiservice")
    .WithReference(db);


builder.Build().Run();
