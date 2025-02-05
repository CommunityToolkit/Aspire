var builder = DistributedApplication.CreateBuilder(args);

var ollama = builder.AddOllama("ollama");

var model = ollama.AddModel("model", "all-minilm:22m");

builder.Build().Run();
