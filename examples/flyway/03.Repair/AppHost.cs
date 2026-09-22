var builder = DistributedApplication.CreateBuilder(args);

// 1. Set path to migration scripts for both Flyway resources
const string migrationScriptsPath = "../database/migrations";

// 2. Add Flyway resource for database migration
var flywayMigration = builder
    .AddFlyway("flywayMigration", migrationScriptsPath);

// 3. Add Flyway resource for database repair
// Repair is run on demand, so we call `WithExplicitStart`
var flywayRepair = builder
    .AddFlyway("flywayRepair", migrationScriptsPath)
    .WithExplicitStart();

// 4. Add Postgres resource with a database, and call `WithFlywayMigration` and `WithFlywayRepair`
// Adminer is added here for convenience to inspect the database after migration
var postgresDb = builder
    .AddPostgres("postgres")
    .WithImageTag("17")
    .WithAdminer()
    .AddDatabase("postgresDb", "space")
    .WithFlywayMigration(flywayMigration)
    .WithFlywayRepair(flywayRepair);

// 5. Let Flyway resources wait for Postgres database to be ready
flywayMigration.WaitFor(postgresDb);
flywayRepair.WaitFor(postgresDb);

builder.Build().Run();
