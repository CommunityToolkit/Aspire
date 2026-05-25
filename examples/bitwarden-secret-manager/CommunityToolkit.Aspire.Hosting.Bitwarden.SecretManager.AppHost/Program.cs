using Projects;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("docker")
    .WithDashboard(false);

var organizationId = builder.AddParameter("bitwarden-organization-id");
var projectName = builder.AddParameter("bitwarden-project-name");
var accessToken = builder.AddParameter("bitwarden-access-token", secret: true);
var demoApiKey = builder.AddParameter("demo-api-key", secret: true);

// Set up a secrets project within the specified organization using the provided management access token.
// The management token MUST have write permissions to the project if it already exists.
// If the project doesn't exist, it will be automatically created with write access for the provided token. 
var bitwarden = builder.AddBitwardenSecretManager("secrets", projectName, organizationId, accessToken);

// Recommended: configure the Bitwarden client with a runtime access token that has fewer privileges than the management token.
bitwarden.WithRuntimeAccessToken(accessToken /* replace with least privilege token */);

// Optional: override the AppHost cache file location.
// This file stores the Bitwarden project ID and secret ID mappings between runs so the integration
// can reuse existing Bitwarden resources rather than creating duplicates.
// By default it is stored in the Aspire store (obj/.aspire/...). Override to share it across workspaces or CI pipelines.
bitwarden.WithCacheFile("demo.json");

// Optional: override the AppHost auth cache file location.
// By default it is stored in the Aspire store alongside the bookkeeping cache.
// Override to reuse a Bitwarden SDK auth session across CI runs or workspaces without re-authenticating.
bitwarden.WithAuthCacheFile(".apphost-auth-cache");

// Add a secret to the project with the value of the demo API key parameter.
// The secret is created or updated on each run. Use `GetSecret` if you only want to read an existing secret.
var demoApiKeySecret = bitwarden.AddSecret("demo-api-key", demoApiKey);

// Register an API service that references the Bitwarden secret manager
// There are two ways to reference secrets from the Bitwarden secret manager in Aspire.
var api = builder.AddProject<CommunityToolkit_Aspire_Hosting_Bitwarden_SecretManager_ApiService>("api")
    .WithHttpHealthCheck("/health");

// 1. Using the secret manager client in code, which allows you to retrieve secrets at runtime and
//    supports dynamic secret retrieval without redeploying the application when secrets change.
// (See ApiService/Program.cs for an example of retrieving secrets from the client in code.)
api.WithReference(bitwarden).WithBitwardenSecretId("DEMO_API_KEY_SECRET_ID", demoApiKeySecret.Resource);

// Optional: persist the app's Bitwarden SDK auth session across restarts so it does not re-authenticate on every startup.
// In run mode a fixed local path is fine; in deployed environments use a parameter so each
// environment can point to a durable storage location (e.g. a mounted volume).
// In deployed environments, set Parameters__bitwarden-auth-cache-location to a persistent path, e.g. /data/bitwarden/auth-cache.
if (builder.ExecutionContext.IsRunMode)
{
    string apiProjectDir = Path.GetDirectoryName(api.Resource.GetProjectMetadata().ProjectPath)!;
    string authCachePath = Path.Combine(apiProjectDir, "obj", ".app-auth-cache");
    api.WithAuthCacheFile(bitwarden, authCachePath);
}
else if (builder.ExecutionContext.IsPublishMode)
{
    api.WithAuthCacheFile(bitwarden, builder.AddParameter("app-auth-cache-location"));
}

// 2. Using direct secret references in the project configuration, which injects the secret value as an environment variable at runtime.
//    This approach is simpler (no Bitwarden code in the application) but requires redeploying the application whenever the secret value changes.
api.WithBitwardenSecretValue("DEMO_API_KEY", demoApiKeySecret.Resource);

// Work around Linux trust-store discovery issues in Bitwarden.Secrets.Sdk 1.0.0.
if (builder.ExecutionContext.IsPublishMode || (builder.ExecutionContext.IsRunMode && OperatingSystem.IsLinux()))
{
    api.WithEnvironment("SSL_CERT_FILE", "/etc/ssl/certs/ca-certificates.crt")
        .WithEnvironment("SSL_CERT_DIR", "/etc/ssl/certs");

    if (builder.ExecutionContext.IsRunMode && OperatingSystem.IsLinux())
    {
        Environment.SetEnvironmentVariable("SSL_CERT_FILE", "/etc/ssl/certs/ca-certificates.crt");
        Environment.SetEnvironmentVariable("SSL_CERT_DIR", "/etc/ssl/certs");
    }
}

builder.Build().Run();