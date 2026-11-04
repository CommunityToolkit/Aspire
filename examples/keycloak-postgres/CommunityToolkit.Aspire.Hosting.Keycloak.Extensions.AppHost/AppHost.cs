using Aspire.Hosting;

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


#pragma warning disable ASPIREDOTNETPROJECT001
builder.AddDotnetProject("project-dev", new Projects.CommunityToolkit_Aspire_Hosting_Keycloak_Extensions_Dev().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
    .WithReference(keycloakDev)
    .WaitFor(keycloakDev);

#pragma warning disable ASPIREDOTNETPROJECT001
builder.AddDotnetProject("project-prod", new Projects.CommunityToolkit_Aspire_Hosting_Keycloak_Extensions_Prod().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
    .WithReference(keycloakProd)
    .WaitFor(keycloakProd);
builder.Build().Run();