var builder = DistributedApplication.CreateBuilder(args);

var password = builder.AddParameter("db-password", "12345678");

var postgres = builder
    .AddPostgres("postgres", password: password, port: 61118)
    .WithLifetime(ContainerLifetime.Persistent);
var postgresdb = postgres.AddDatabase("postgresdb");

var umami = builder
    .AddUmami("umami", port: 55932)
    .WithStorageBackend(postgresdb);

var front = builder
    .AddJavaScriptApp("front", "../umami-vite-app");

builder.Build().Run();