var builder = DistributedApplication.CreateBuilder(args);

// The CPU build of vLLM with a tiny model keeps this test runnable on CI agents without a GPU.
builder.AddVLLM("vllm")
    .WithImage("vllm/vllm-openai-cpu", "v0.33.0")
    .WithModel("facebook/opt-125m")
    .WithArgs("--max-model-len", "512")
    // The CPU build otherwise reserves most of the host memory for the KV cache (in GiB).
    .WithEnvironment("VLLM_CPU_KVCACHE_SPACE", "1");

builder.Build().Run();
