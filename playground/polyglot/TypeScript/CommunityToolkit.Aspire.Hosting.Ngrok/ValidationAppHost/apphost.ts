import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

const authToken = await builder.addParameterWithValue("ngrok-auth-token", "ngrok-token-value", { secret: true });

const upstream = await builder
    .addContainer("upstream", "nginx:alpine")
    .withHttpEndpoint({ name: "http", targetPort: 80 });

await builder
    .addNgrok("ngrok-parameter", {
        configurationFolder: ".ngrok-parameter",
        endpointPort: 59600,
        endpointName: "http",
        configurationVersion: 3
    })
    .withAuthToken(authToken)
    .withTunnelEndpoint(upstream, "http", {
        ngrokUrl: "https://parameter.ngrok.test",
        labels: {
            environment: "dev",
            service: "upstream"
        }
    });

const ngrokValue = builder.addNgrok("ngrok-value");
await ngrokValue.withAuthTokenValue("plain-text-token");
await ngrokValue.withTunnelEndpoint(upstream, "http");

await builder.build().run();
