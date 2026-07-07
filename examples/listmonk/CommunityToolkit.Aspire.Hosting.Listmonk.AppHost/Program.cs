var builder = DistributedApplication.CreateBuilder(args);

var password = builder.AddParameter("db-password", "12345678");
var adminPassword = builder.AddParameter("admin-password", "SuperSecret123!", secret: true);

var postgres = builder
    .AddPostgres("postgres", password: password)
    .WithLifetime(ContainerLifetime.Persistent);

var listmonkDb = postgres.AddDatabase("listmonkdb");

builder.AddListmonk("listmonk")
    .WithPostgreSQL(listmonkDb)
    .WithAdminCredentials("admin", adminPassword)
    .WithDatabaseMaxOpenConnections(25)
    .WithDatabaseMaxIdleConnections(25)
    .WithDatabaseMaxLifetime("300s")
    .WithTimeZone("Etc/UTC")
    .WithUploadsVolume();

builder.Build().Run();
