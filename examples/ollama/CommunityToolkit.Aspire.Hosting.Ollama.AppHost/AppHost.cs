var builder = DistributedApplication.CreateBuilder(args);

var ollama = builder.AddOllamaLocal("ollama")
    .WithOpenWebUI();

var phi3 = ollama.AddModel("phi3", "phi3");
var llama = ollama.AddHuggingFaceModel("llama", "bartowski/Llama-3.2-1B-Instruct-GGUF:IQ4_XS");

var ollama2 = builder.AddOllama("ollama2")
    .WithDataVolume()
    .WithOpenWebUI();

var tinyllama = ollama2.AddModel("tinyllama", "tinyllama");

#pragma warning disable ASPIREDOTNETPROJECT001
builder.AddDotnetProject("webfrontend", new Projects.CommunityToolkit_Aspire_Hosting_Ollama_Web().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
    .WithExternalHttpEndpoints()
    .WithReference(phi3)
    .WaitFor(phi3)
    .WithReference(llama)
    .WithReference(tinyllama);

builder.Build().Run();
