var builder = DistributedApplication.CreateBuilder(args);

// 1. Create configuration for Flyway resource
// Can be useful to reuse the configuration across multiple Flyway resources, or group the settings
var flywayConfiguration =
    new FlywayResourceConfiguration
    {
        ImageTag = "11",
        MigrationScriptsPath = "../database/migrations",
    };

// 2. Add Flyway resource, passing the configuration created above
var flywayMigration = builder
    .AddFlyway("flywayMigration", flywayConfiguration);

// 3. Add Postgres resource with a database, and call `WithFlywayMigration`
// Adminer is added here for convenience to inspect the database after migration
var postgresDb = builder
    .AddPostgres("postgres")
    .WithImageTag("17")
    .WithAdminer()
    .AddDatabase("postgresDb", "space")
    .WithFlywayMigration(flywayMigration);

// 4. Let Flyway wait for Postgres database to be ready
flywayMigration.WaitFor(postgresDb);

builder.Build().Run();
