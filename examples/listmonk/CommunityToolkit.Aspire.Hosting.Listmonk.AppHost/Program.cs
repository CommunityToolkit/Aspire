var builder = DistributedApplication.CreateBuilder(args);

var adminPassword = builder.AddParameter("admin-password", "SuperSecret123!", secret: true);
var database = builder.AddPostgres("postgres")
    .AddDatabase("listmonkdb");

builder.AddListmonk("listmonk")
    .WithReference(database)
    .WithAdminCredentials("admin", adminPassword)
    .WithDatabaseMaxOpenConnections(25)
    .WithDatabaseMaxIdleConnections(25)
    .WithDatabaseMaxLifetime("300s")
    .WithTimeZone("Etc/UTC")
    .WithUploadsVolume();

builder.Build().Run();
