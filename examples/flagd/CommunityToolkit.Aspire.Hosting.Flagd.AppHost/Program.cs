using OpenFeature.Contrib.Providers.Flagd;

var builder = DistributedApplication.CreateBuilder(args);

// Add flagd with local flag configuration file
var flagd = builder
    .AddFlagd("flagd")
    .WithBindFileSync("./flags/")
    .WithLogging();

builder.Build().Run();
