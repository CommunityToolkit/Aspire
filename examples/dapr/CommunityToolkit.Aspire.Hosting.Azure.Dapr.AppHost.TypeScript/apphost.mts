import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const cae = await builder.addAzureContainerAppEnvironment("cae");
await cae.withDaprComponents();

const stateStore = await builder
    .addDaprStateStore("statestore")
    .withMetadata("actorStateStore", "true");
const pubSub = await builder.addDaprPubSub("pubsub");

const serviceA = await builder
    .addProject("servicea", "../CommunityToolkit.Aspire.Hosting.Dapr.ServiceA/CommunityToolkit.Aspire.Hosting.Dapr.ServiceA.csproj")
    .publishAsAzureContainerApp(async () => { })
    .withDaprSidecar()
    .withReference(stateStore)
    .withReference(pubSub);

await builder
    .addProject("serviceb", "../CommunityToolkit.Aspire.Hosting.Dapr.ServiceB/CommunityToolkit.Aspire.Hosting.Dapr.ServiceB.csproj")
    .withDaprSidecar()
    .withReference(pubSub)
    .waitFor(serviceA);

await builder
    .addProject("servicec", "../CommunityToolkit.Aspire.Hosting.Dapr.ServiceC/CommunityToolkit.Aspire.Hosting.Dapr.ServiceC.csproj")
    .withDaprSidecar()
    .withReference(stateStore)
    .waitFor(serviceA);

await builder.build().run();
