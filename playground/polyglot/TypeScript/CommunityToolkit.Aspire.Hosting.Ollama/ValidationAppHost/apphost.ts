import { createBuilder, OllamaGpuVendor } from './.modules/aspire.js';

const builder = await createBuilder();

// Keep one practical runtime path for optional smoke validation.
const runtimeOllama = builder.addOllama('ollama-runtime');

const runtimeOllamaResource = await runtimeOllama;
const _runtimeModels = await runtimeOllamaResource.models.get();
const _runtimePrimaryEndpoint = await runtimeOllamaResource.getEndpoint('http');
const _runtimeHost = await _runtimePrimaryEndpoint.host();
const _runtimePort = await _runtimePrimaryEndpoint.port();
const _runtimeUri = await _runtimePrimaryEndpoint.url();
const _runtimeConnectionString = await runtimeOllamaResource.connectionStringExpression.get();

// Compile-time coverage for the broader exported surface, including variants that are not
// practical to run in this environment (local executable, model downloads, and GPU options).
const includeCompileOnlyScenarios = false;

if (includeCompileOnlyScenarios) {
    const containerOllama = builder.addOllama('ollama-polyglot', { port: 11436 });
    await containerOllama.withDataVolume({ name: 'ollama-data' });
    await containerOllama.withGPUSupport({ vendor: OllamaGpuVendor.AMD });

    const autoNamedModel = containerOllama.addModel('tinyllama');
    const namedModel = containerOllama.addNamedModel('phi3-model', 'phi3');
    const huggingFaceModel = containerOllama.addHuggingFaceModel('llama-hf', 'bartowski/Llama-3.2-1B-Instruct-GGUF:IQ4_XS');

    await containerOllama.withOpenWebUI({
        containerName: 'ollama-polyglot-openwebui',
        configureContainer: async (openWebUI) => {
            await openWebUI.withDataVolume({ name: 'openwebui-data' });
            await openWebUI.withHostPort({ port: 3001 });

            const _openWebUiPrimaryEndpoint = await openWebUI.getEndpoint('http');
            const _openWebUiHost = await _openWebUiPrimaryEndpoint.host();
            const _openWebUiPort = await _openWebUiPrimaryEndpoint.port();
            const _openWebUiUri = await _openWebUiPrimaryEndpoint.url();
            const _openWebUiConnectionString = await openWebUI.connectionStringExpression.get();
            const _openWebUiOllamas = await openWebUI.ollamaResources.get();
        }
    });

    const resolvedContainerOllama = await containerOllama;
    const _containerModels = await resolvedContainerOllama.models.get();
    const _containerPrimaryEndpoint = await resolvedContainerOllama.getEndpoint('http');
    const _containerHost = await _containerPrimaryEndpoint.host();
    const _containerPort = await _containerPrimaryEndpoint.port();
    const _containerUri = await _containerPrimaryEndpoint.url();
    const _containerConnectionString = await resolvedContainerOllama.connectionStringExpression.get();

    const resolvedAutoNamedModel = await autoNamedModel;
    const _autoNamedModelParent = await resolvedAutoNamedModel.parent.get();
    const _autoNamedModelName = await resolvedAutoNamedModel.modelName.get();
    const _autoNamedModelConnectionString = await resolvedAutoNamedModel.connectionStringExpression.get();

    const resolvedNamedModel = await namedModel;
    const _namedModelParent = await resolvedNamedModel.parent.get();
    const _namedModelName = await resolvedNamedModel.modelName.get();
    const _namedModelConnectionString = await resolvedNamedModel.connectionStringExpression.get();

    const resolvedHuggingFaceModel = await huggingFaceModel;
    const _huggingFaceModelParent = await resolvedHuggingFaceModel.parent.get();
    const _huggingFaceModelName = await resolvedHuggingFaceModel.modelName.get();
    const _huggingFaceModelConnectionString = await resolvedHuggingFaceModel.connectionStringExpression.get();

    const localOllama = builder.addOllamaLocal('ollama-local', { targetPort: 11435 });
    const localAutoNamedModel = localOllama.addModel('phi3');

    await localOllama.withOpenWebUI({
        containerName: 'ollama-local-openwebui',
        configureContainer: async (openWebUI) => {
            await openWebUI.withHostPort({ port: 3002 });
        }
    });

    const resolvedLocalOllama = await localOllama;
    const _localModels = await resolvedLocalOllama.models.get();
    const _localPrimaryEndpoint = await resolvedLocalOllama.getEndpoint('http');
    const _localHost = await _localPrimaryEndpoint.host();
    const _localPort = await _localPrimaryEndpoint.port();
    const _localUri = await _localPrimaryEndpoint.url();
    const _localConnectionString = await resolvedLocalOllama.connectionStringExpression.get();

    const resolvedLocalAutoNamedModel = await localAutoNamedModel;
    const _localAutoNamedModelParent = await resolvedLocalAutoNamedModel.parent.get();
    const _localAutoNamedModelName = await resolvedLocalAutoNamedModel.modelName.get();
    const _localAutoNamedModelConnectionString = await resolvedLocalAutoNamedModel.connectionStringExpression.get();
}

await builder.build().run();
