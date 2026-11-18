var builder = DistributedApplication.CreateBuilder(args);

// vLLM serves an OpenAI-compatible API at "{endpoint}/v1". Resources that wait for it start
// only after its /health check passes, which happens once the model has finished loading.
var vllm = builder.AddVLLM("vllm")
    .WithGPUSupport()
    .WithDataVolume();

var model = vllm.AddModel(name: "qwen", model: "Qwen/Qwen3-0.6B");

#pragma warning disable ASPIREDOTNETPROJECT001
builder.AddDotnetProject("consumer", new Projects.CommunityToolkit_Aspire_Hosting_VLLM_ConsumerApp().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
    .WithReference(model)
    .WaitFor(model);

builder.Build().Run();
