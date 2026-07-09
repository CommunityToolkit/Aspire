import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const perlApi = await builder.addPerlApi("perl-api", ".", "../scripts/API.pl");
await perlApi.withCpanMinus();
await perlApi.withPackage("Mojolicious::Lite", { force: true, skipTest: true });
await perlApi.withLocalLib({ path: "local" });
await perlApi.withHttpEndpoint({ name: "http", env: "PORT" });

const perlDriver = await builder.addPerlScript("perl-driver", "../scripts", "driver.pl");
await perlDriver.withEnvironment("API_URL", perlApi.getEndpoint("http"));
await perlDriver.withReference(perlApi);
await perlDriver.waitFor(perlApi);

await builder.build().run();