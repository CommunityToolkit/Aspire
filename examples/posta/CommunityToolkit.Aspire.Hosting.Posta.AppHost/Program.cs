var builder = DistributedApplication.CreateBuilder(args);

var postgresPassword = builder.AddParameter("postgres-password", "posta");
var postgres = builder.AddPostgres("postgres", password: postgresPassword);
var database = postgres.AddDatabase("posta-db", "posta");
var redis = builder.AddRedis("redis");

builder.AddPosta("posta", database, redis)
    .WithDataVolume();

builder.Build().Run();
