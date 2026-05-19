using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var organizationId = builder.AddParameter("bitwarden-organization-id");
var projectName = builder.AddParameter("bitwarden-project-name");
var accessToken = builder.AddParameter("bitwarden-access-token", secret: true);
var demoApiKey = builder.AddParameter("demo-api-key", secret: true);

var bitwarden = builder.AddBitwardenSecretManager("bitwarden", projectName, organizationId, accessToken);
bitwarden.AddSecret("demo-api-key", demoApiKey);

builder.AddProject<CommunityToolkit_Aspire_Hosting_Bitwarden_SecretManager_ApiService>("api")
    .WithReference(bitwarden)
    .WaitFor(bitwarden)
    .WithHttpHealthCheck("/health");

builder.Build().Run();