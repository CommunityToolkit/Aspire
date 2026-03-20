var builder = DistributedApplication.CreateBuilder(args);

builder.AddPerlScript("cpan-worker", "../scripts", "Worker.pl")
    .WithPackage("OpenTelemetry::SDK", skipTest: true, force: true);
    //.WithLocalLib("local"); //would automatically swap to Cpanm, may fix at a later date, documentation is inconclusive for how to wire together.

builder.Build().Run();
