import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const ollama = await builder.addOllama("ollama");
await ollama.withOpenWebUI();

const phi3 = await ollama.addNamedModel("phi3", "phi3");
const llama = await ollama.addHuggingFaceModel(
    "llama",
    "bartowski/Llama-3.2-1B-Instruct-GGUF:IQ4_XS",
);

const ollama2 = await builder.addOllama("ollama2");
await ollama2.withDataVolume();
await ollama2.withOpenWebUI();

const tinyllama = await ollama2.addNamedModel("tinyllama", "tinyllama");

await phi3.connectionStringExpression();
await llama.connectionStringExpression();
await tinyllama.connectionStringExpression();

await builder.build().run();
