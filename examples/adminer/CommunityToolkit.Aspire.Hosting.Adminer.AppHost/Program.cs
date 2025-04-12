var builder = DistributedApplication.CreateBuilder(args);

var postgres1 = builder.AddPostgres("postgres1")
    .WithAdminer();
postgres1.AddDatabase("db1");
postgres1.AddDatabase("db2");

builder.Build().Run();