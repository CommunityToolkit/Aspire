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
    .WithBitwardenSecretId("DEMO_API_KEY_SECRET_ID", demoApiKeySecret.Resource)
    .WithBitwardenSecretValue("DEMO_API_KEY", demoApiKeySecret.Resource)
    .WithHttpHealthCheck("/health");

if (OperatingSystem.IsLinux())
{
    // Work around Linux trust-store discovery issues in Bitwarden.Secrets.Sdk 1.0.0.
    api.WithEnvironment("SSL_CERT_FILE", "/etc/ssl/certs/ca-certificates.crt")
        .WithEnvironment("SSL_CERT_DIR", "/etc/ssl/certs");
}

builder.Build().Run();