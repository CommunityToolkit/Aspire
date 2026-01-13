var builder = DistributedApplication.CreateBuilder(args);

// 1. Add Flyway resource
var flywayMigration = builder
    .AddFlyway("flywayMigration", "../database/migrations");

// 2. Add Postgres resource with a database, and call `WithFlywayMigration`
// Adminer is added here for convenience to inspect the database after migration
var postgresDb = builder
    .AddPostgres("postgres")
    .WithImageTag("17")
    .WithAdminer()
    .AddDatabase("postgresDb", "space")
    .WithFlywayMigration(flywayMigration);

// 3. Let Flyway wait for Postgres database to be ready
flywayMigration.WaitFor(postgresDb);

builder.Build().Run();
