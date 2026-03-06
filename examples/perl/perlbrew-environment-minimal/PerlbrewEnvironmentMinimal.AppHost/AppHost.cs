var builder = DistributedApplication.CreateBuilder(args);

builder.AddPerlScript("perlbrew-worker", "../scripts", "Worker.pl")
    .WithPerlbrewEnvironment("perl-5.38.0");

builder.AddPerlScript("validation-worker", "../scripts", "ValidatePerlVersion.pl");

builder.Build().Run();
