var builder = DistributedApplication.CreateBuilder(args);

// The CPU build of vLLM with a tiny model keeps this test runnable on CI agents without a GPU.
var vllm = builder.AddVLLM("vllm")
    .WithImage("vllm/vllm-openai-cpu", "v0.33.0")
    .WithServedModelName("opt")
    // Avoid CPU JIT warmup on small CI runners.
    .WithArgs("--max-model-len", "512", "--enforce-eager")
    // The CPU build otherwise reserves most of the host memory for the KV cache (in GiB).
    .WithEnvironment("VLLM_CPU_KVCACHE_SPACE", "1");

var model = vllm.AddModel("model", "facebook/opt-125m");

builder.AddExecutable("model-consumer", "dotnet", ".")
    .WithArgs("--version")
    .WithReference(model)
    .WaitFor(model);

builder.Build().Run();
