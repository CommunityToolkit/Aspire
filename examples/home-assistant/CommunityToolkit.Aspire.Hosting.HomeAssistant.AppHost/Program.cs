var builder = DistributedApplication.CreateBuilder(args);

builder.AddHomeAssistant("home-assistant", 7420)
       .WithDataVolume()
       .WithLifetime(ContainerLifetime.Persistent);

builder.Build().Run();
