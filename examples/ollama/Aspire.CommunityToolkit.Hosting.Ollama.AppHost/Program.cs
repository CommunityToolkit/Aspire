var builder = DistributedApplication.CreateBuilder(args);

var ollama = builder.AddOllama("ollama", port: 11434)
    .AddModel("phi3.5")
    .WithDefaultModel("phi3.5")
    .WithOpenWebUI();

builder.AddProject<Projects.Aspire_CommunityToolkit_Hosting_Ollama_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(ollama)
    .WithEnvironment("ollama:model", "phi3.5");

builder.AddProject<Projects.Aspire_CommunityToolkit_Hosting_Ollama_Web_MEAI>("webfrontendmeai")
    .WithExternalHttpEndpoints()
    .WithReference(ollama)
    .WithEnvironment("ollama:model", "phi3.5");

builder.Build().Run();
