var builder = DistributedApplication.CreateBuilder(args);

builder.AddPerlScript("cpan-worker", "../scripts", "Worker.pl")
    .WithPackage("OpenTelemetry::SDK", skipTest: true);

builder.Build().Run();
