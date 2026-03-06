var builder = DistributedApplication.CreateBuilder(args);

builder.AddPerlScript("cpan-worker", "../scripts", "Worker.pl")
    .WithPackage("OpenTelemetry::SDK", skipTest: true)
    .WithLocalLib("local"); //to avoid 'sudo' applying to system perl.

builder.Build().Run();
