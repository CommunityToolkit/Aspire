var builder = DistributedApplication.CreateBuilder(args);

var perlApi = builder.AddPerlApi("perl-api", ".", "../scripts/API.pl")
    .WithCpanMinus()
    .WithPackage("Mojolicious::Lite", force: true, skipTest: true)
    .WithLocalLib("local") //to avoid 'sudo' applying to system perl.   
    .WithHttpEndpoint(name: "http", env: "PORT");

builder.AddPerlScript("perl-driver", "../scripts", "driver.pl")
    .WithEnvironment("API_URL", perlApi.GetEndpoint("http"))
    .WithReference(perlApi)
    .WaitFor(perlApi);

builder.Build().Run();
