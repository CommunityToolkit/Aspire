var builder = DistributedApplication.CreateBuilder(args);

builder.AddOllamaLocal("ollama", targetPort: 11435).AddModel("tinyllama");

builder.AddOllama("ollama2").AddModel("tinyllama");

builder.Build().Run();
