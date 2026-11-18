import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

// The CPU build of vLLM with a tiny public model keeps this example runnable without a GPU.
// On a machine with an NVIDIA or AMD GPU, use the default image and call withGPUSupport().
// For gated models, pass a Hugging Face token parameter with withHuggingFaceToken().
const vllm = await builder.addVLLM("vllm");
await vllm.withImage("vllm/vllm-openai-cpu", { tag: "v0.33.0" });
await vllm.withDataVolume();
const model = await vllm.addNamedModel("model", "facebook/opt-125m");
await vllm.withServedModelName("opt");
// Avoid JIT warmup for this CPU-only example.
await vllm.withArgs(["--enforce-eager"]);
// The CPU build otherwise reserves most of the host memory for the KV cache (in GiB).
await vllm.withEnvironment("VLLM_CPU_KVCACHE_SPACE", "1");

await model.connectionStringExpression();

await builder.build().run();
