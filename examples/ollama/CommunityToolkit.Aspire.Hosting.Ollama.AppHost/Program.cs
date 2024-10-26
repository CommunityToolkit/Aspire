var builder = DistributedApplication.CreateBuilder(args);

var ollama = builder.AddOllama("ollama")
    .WithDefaultModel("phi:chat")
    .WithOpenWebUI();

var llama = ollama.AddHuggingFaceModel("llama", "bartowski/Llama-3.2-1B-Instruct-GGUF:IQ4_XS");

builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Ollama_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(ollama) // uses default model
    .WithReference(llama);

builder.Build().Run();
