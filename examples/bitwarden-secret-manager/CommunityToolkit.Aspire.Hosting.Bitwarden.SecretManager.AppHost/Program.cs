using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var organizationId = builder.AddParameter("bitwarden-organization-id");
var projectName = builder.AddParameter("bitwarden-project-name");
var accessToken = builder.AddParameter("bitwarden-access-token", secret: true);
var demoApiKey = builder.AddParameter("demo-api-key", secret: true);

var bitwarden = builder.AddBitwardenSecretManager("bitwarden", projectName, organizationId, accessToken);
var demoApiKeySecret = bitwarden.AddSecret("demo-api-key", demoApiKey);

var api = builder.AddProject<CommunityToolkit_Aspire_Hosting_Bitwarden_SecretManager_ApiService>("api")
    .WithReference(bitwarden)
    .WithEnvironment(env =>
    {
        // Pass the resolved Bitwarden secret ID so the sample service can fetch the
        // secret directly from Bitwarden using the client integration.
        env.EnvironmentVariables["DEMO_API_KEY_SECRET_ID"] = demoApiKeySecret.Resource.SecretId!;

        // Or pass secret value directly (less secure, but hey, it's just a sample!).
        env.EnvironmentVariables["DEMO_API_KEY"] = demoApiKeySecret.Resource.Value;
    })
    .WaitFor(bitwarden)
    .WithHttpHealthCheck("/health");

builder.Build().Run();