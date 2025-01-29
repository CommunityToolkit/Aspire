var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDbGate(c=> c.WithHostPort(8068));
postgres.AddDatabase("db1");
postgres.AddDatabase("db2");


builder.Build().Run();
