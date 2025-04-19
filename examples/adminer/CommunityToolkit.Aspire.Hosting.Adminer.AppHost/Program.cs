var builder = DistributedApplication.CreateBuilder(args);

var postgres1 = builder.AddPostgres("postgres1")
    .WithAdminer();
postgres1.AddDatabase("db1");
postgres1.AddDatabase("db2");

var postgres2 = builder.AddPostgres("postgres2")
    .WithAdminer();
postgres2.AddDatabase("db3");
postgres2.AddDatabase("db4");

builder.Build().Run();