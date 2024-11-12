var builder = DistributedApplication.CreateBuilder(args);

var ollama = builder.AddOllama("ollama");

var tinyllama = ollama.AddModel("tinyllama");

builder.Build().Run();
