var builder = DistributedApplication.CreateBuilder(args);

var ollama = builder.AddOllama("ollama", modelName: "phi3");

builder.AddProject<Projects.Aspire_CommunityToolkit_Hosting_Ollama_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(ollama)
    .WithEnvironment("ollama:model", "phi3");

builder.Build().Run();
