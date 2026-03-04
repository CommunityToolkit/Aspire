var builder = DistributedApplication.CreateBuilder(args);

builder.AddPerlScript("cpanm-worker", "../scripts", "Worker.pl")
    .WithCpanMinus()
    .WithPackage("OpenTelemetry::SDK", skipTest: true);

builder.Build().Run();
