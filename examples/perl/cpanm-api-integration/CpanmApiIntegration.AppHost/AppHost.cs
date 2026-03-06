var builder = DistributedApplication.CreateBuilder(args);

builder.AddPerlApi("perl-api", ".", "../scripts/API.pl")
    .WithCpanMinus()
    .WithPackage("Mojolicious::Lite")
    .WithHttpEndpoint(name: "http", env: "PORT");

builder.Build().Run();
