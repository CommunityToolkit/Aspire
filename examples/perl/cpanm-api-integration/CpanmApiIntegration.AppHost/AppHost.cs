var builder = DistributedApplication.CreateBuilder(args);

builder.AddPerlApi("perl-api", ".", "../scripts/API.pl")
    .WithCpanMinus()
    .WithPackage("Mojolicious::Lite", force: true, skipTest: true)
    .WithLocalLib("local") //to avoid 'sudo' applying to system perl.   
    .WithHttpEndpoint(name: "http", env: "PORT");

builder.Build().Run();
