import { CertificateTrustScope, createBuilder } from "./.modules/aspire.js";

const builder = await createBuilder();

const cartonProjectApi = await builder.addPerlApi("perl-api", "../scripts", "API.pl");
await cartonProjectApi.withCarton();
await cartonProjectApi.withProjectDependencies({ cartonDeployment: true });
await cartonProjectApi.withLocalLib({ path: "local" });
await cartonProjectApi.withDeveloperCertificateTrust(true);
await cartonProjectApi.withCertificateTrustScope(CertificateTrustScope.Append);
await cartonProjectApi.withPerlCertificateTrust();
await cartonProjectApi.withHttpEndpoint({ name: "http", env: "PORT" });

const perlDriver = await builder.addPerlScript("perl-driver", "../scripts", "driver.pl");
await perlDriver.withEnvironment("API_URL", cartonProjectApi.getEndpoint("http"));
await perlDriver.withReference(cartonProjectApi);
await perlDriver.waitFor(cartonProjectApi);

await builder.build().run();