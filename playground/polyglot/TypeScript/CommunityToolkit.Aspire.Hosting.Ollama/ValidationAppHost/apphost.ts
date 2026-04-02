import { createBuilder, OllamaGpuVendor } from './.modules/aspire.js';

const builder = await createBuilder();

// Keep one practical runtime path for optional smoke validation.
const runtimeOllama = builder.addOllama('ollama-runtime');

const runtimeOllamaResource = await runtimeOllama;
const _runtimeModels = await runtimeOllamaResource.models.get();
const _runtimePrimaryEndpoint = await runtimeOllamaResource.primaryEndpoint.get();
const _runtimeHost = await runtimeOllamaResource.host.get();
const _runtimePort = await runtimeOllamaResource.port.get();
const _runtimeUri = await runtimeOllamaResource.uriExpression.get();
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

            const _openWebUiPrimaryEndpoint = await openWebUI.primaryEndpoint.get();
            const _openWebUiHost = await openWebUI.host.get();
            const _openWebUiPort = await openWebUI.port.get();
            const _openWebUiUri = await openWebUI.uriExpression.get();
            const _openWebUiConnectionString = await openWebUI.connectionStringExpression.get();
            const _openWebUiOllamas = await openWebUI.ollamaResources.get();
        }
    });

    const resolvedContainerOllama = await containerOllama;
    const _containerModels = await resolvedContainerOllama.models.get();
    const _containerPrimaryEndpoint = await resolvedContainerOllama.primaryEndpoint.get();
    const _containerHost = await resolvedContainerOllama.host.get();
    const _containerPort = await resolvedContainerOllama.port.get();
    const _containerUri = await resolvedContainerOllama.uriExpression.get();
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
    const _localPrimaryEndpoint = await resolvedLocalOllama.primaryEndpoint.get();
    const _localHost = await resolvedLocalOllama.host.get();
    const _localPort = await resolvedLocalOllama.port.get();
    const _localUri = await resolvedLocalOllama.uriExpression.get();
    const _localConnectionString = await resolvedLocalOllama.connectionStringExpression.get();

    const resolvedLocalAutoNamedModel = await localAutoNamedModel;
    const _localAutoNamedModelParent = await resolvedLocalAutoNamedModel.parent.get();
    const _localAutoNamedModelName = await resolvedLocalAutoNamedModel.modelName.get();
    const _localAutoNamedModelConnectionString = await resolvedLocalAutoNamedModel.connectionStringExpression.get();
}

await builder.build().run();
