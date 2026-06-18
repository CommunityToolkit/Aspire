import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const rabbitMq = await builder.addContainer("rmq", {
    image: "rabbitmq",
    tag: "4-management",
});

await rabbitMq.withEndpoint({
    name: "amqp",
    port: 5672,
    targetPort: 5672,
    scheme: "amqp",
});

await rabbitMq.withEndpoint({
    name: "management",
    port: 15672,
    targetPort: 15672,
    scheme: "http",
    isExternal: true,
});

const api = await builder.addProject(
    "api",
    "../CommunityToolkit.Aspire.MassTransit.RabbitMQ.ApiService/CommunityToolkit.Aspire.MassTransit.RabbitMQ.ApiService.csproj",
);
await api.waitFor(rabbitMq);
await api.withReference(rabbitMq);

const publisher = await builder.addProject(
    "publisher",
    "../CommunityToolkit.Aspire.MassTransit.RabbitMQ.Publisher/CommunityToolkit.Aspire.MassTransit.RabbitMQ.Publisher.csproj",
);
await publisher.waitFor(api);
await publisher.waitFor(rabbitMq);
await publisher.withReference(rabbitMq);

await builder.build().run();
