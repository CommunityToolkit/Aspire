var builder = DistributedApplication.CreateBuilder(args);

builder.AddPerlScript("cpanm-worker", "../scripts", "Worker.pl")
    .WithCpanMinus()
    .WithPackage("OpenTelemetry::SDK", skipTest: true)
    .WithLocalLib("local"); //to avoid 'sudo' applying to system perl.    
    
builder.Build().Run();
