var builder = DistributedApplication.CreateBuilder(args);

// Add flagd with local flag configuration file
var flagd = builder.AddFlagd("flagd")
    .WithFlagConfigurationFile("flags.json", "/etc/flagd/flags.json")
    .WithLogging("debug");

builder.Build().Run();
