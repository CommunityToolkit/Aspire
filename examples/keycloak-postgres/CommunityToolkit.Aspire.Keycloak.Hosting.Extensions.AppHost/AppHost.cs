using Aspire.Hosting;
using CommunityToolkit.Aspire.Keycloak.Extensions;


var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("keycloak-postgres-dev");
var dbDev = postgres.AddDatabase("db-dev");

var keycloakDev = builder.AddKeycloak("keycloak-dev")
    .WithPostgres(dbDev);

var dbUserName = builder.AddParameter("db-username", "postgres");
var dbPassword = builder.AddParameter("db-password", "Postgres!123");

var postgresProd = builder.AddPostgres("postgres-prod",
    dbUserName, dbPassword);

var dbProd = postgresProd.AddDatabase("db-prod");

var keycloakProd = builder.AddKeycloak("keycloak-prod")
    .WithPostgres(dbProd, dbUserName, dbPassword);


builder.AddProject<Projects.CommunityToolkit_Aspire_Keycloak_Hosting_Extensions_Dev>("project-dev")
    .WithReference(keycloakDev)
    .WaitFor(keycloakDev);

builder.AddProject<Projects.CommunityToolkit_Aspire_Keycloak_Hosting_Extensions_Prod>("project-prod")
    .WithReference(keycloakProd)
    .WaitFor(keycloakProd);
builder.Build().Run();