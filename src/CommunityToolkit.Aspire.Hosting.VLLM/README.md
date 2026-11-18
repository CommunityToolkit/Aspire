# CommunityToolkit.Aspire.Hosting.VLLM library

Provides extension methods and resource definitions for the Aspire AppHost to run [vLLM](https://github.com/vllm-project/vllm), a high-throughput, OpenAI-compatible LLM inference server, in a container. The package supports GPU acceleration and serving models from Hugging Face.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.VLLM
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define a vLLM resource by calling `AddVLLM`:

```csharp
var hfToken = builder.AddParameter("hf-token", secret: true);

var vllm = builder.AddVLLM("vllm")
    .WithGPUSupport()               // NVIDIA by default; pass VLLMGpuVendor.AMD for ROCm
    .WithDataVolume()               // persist the Hugging Face model cache
    .WithHuggingFaceToken(hfToken); // only needed for gated models

var model = vllm.AddModel(name: "qwen", model: "Qwen/Qwen3-0.6B");

builder.AddProject<Projects.MyApi>("api")
    .WithReference(model)
    .WaitFor(model);
```

Each vLLM server serves one base model. Call `AddModel` once per server; to serve another model, add another vLLM resource. A server without a model fails when its launch arguments are evaluated. The single-argument `AddModel("Qwen/Qwen3-0.6B")` overload generates a resource name from the server and model names.

vLLM exposes an OpenAI-compatible API at `{endpoint}/v1`. The server's health check uses vLLM's `/health` endpoint. That endpoint returns `200` only after the model has loaded, so resources that call `WaitFor(model)` wait for the parent server to become healthy.

The model resource's connection string has the form `Endpoint=http://host:port;Model=Qwen/Qwen3-0.6B`. If `.WithServedModelName("qwen")` is configured on the server, `Model` contains the served alias instead. Use the `CommunityToolkit.Aspire.VLLM` client integration to consume the model resource as an `IChatClient`. A direct server reference supplies only `Endpoint` and requires the client to configure a model separately.

vLLM's engine processes communicate through shared memory, so the container starts with `--shm-size 8g`. The container runtime's default `/dev/shm` is too small for the engine to start. To override the size, call `WithContainerRuntimeArgs("--shm-size", "16g")`. For multi-GPU tensor parallelism, `WithContainerRuntimeArgs("--ipc", "host")` is an alternative.

To run without a GPU, use vLLM's CPU image, for example `.WithImage("vllm/vllm-openai-cpu", "v0.33.0")`. The CPU image reserves most of the host memory for the KV cache by default; set `VLLM_CPU_KVCACHE_SPACE` (in GiB) with `.WithEnvironment("VLLM_CPU_KVCACHE_SPACE", "1")` to limit it.

For low-resource CPU development or smoke tests, `.WithArgs("--enforce-eager")` can reduce startup time by disabling graph compilation and JIT warmup. This may reduce inference throughput compared with compiled execution.

## Additional Information

- https://docs.vllm.ai
- https://learn.microsoft.com/dotnet/aspire/community-toolkit/

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
