var builder = DistributedApplication.CreateBuilder(args);

var ollama = builder.AddOllama("ollama", port: null)
    .AddModel("phi3")
    .WithDefaultModel("phi3")
    .WithOpenWebUI();

builder.AddProject<Projects.Aspire_CommunityToolkit_Hosting_Ollama_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(ollama)
    .WithEnvironment("ollama:model", "phi3");

builder.Build().Run();
