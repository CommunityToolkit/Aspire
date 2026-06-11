using Aspire.Hosting.Docker.Resources.ServiceNodes;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var compose = builder.AddDockerComposeEnvironment("compose")
    .WithDashboard(false);

var organizationId = builder.AddParameter("bitwarden-organization-id");
var project = builder.AddParameter("bitwarden-project");
var accessToken = builder.AddParameter("bitwarden-access-token", secret: true);

// Set up a secrets project within the specified organization using the provided management access token.
// Project can be specified by name (for managed projects) or ID (for existing projects).
// The management token MUST have write permissions to the project if it already exists.
// If the project doesn't exist, it will be automatically created with write access for the provided token.
var bitwarden = builder.AddBitwardenSecretManager("secrets", project, organizationId, accessToken);

// For self-hosted installations, configure your API and Identity URLs here.
// (Self-hosting requires an enterprise plan, so this example uses the default cloud-hosted Bitwarden instance.)
var bitwardenApiServer = builder.AddExternalService("bitwarden-api", "https://api.bitwarden.com");
var bitwardenIdentityServer = builder.AddExternalService("bitwarden-identity", "https://identity.bitwarden.com");

bitwarden.WithApiUrl(bitwardenApiServer)
    .WithIdentityUrl(bitwardenIdentityServer);

// Optional: override the AppHost cache file location.
// The cache stores the Bitwarden project ID and secret ID mappings between runs so the integration
// can reuse existing Bitwarden resources rather than creating duplicates.
// Override to share the cache across multiple AppHost projects, or to store it in a CI cache directory.
// Relative paths are resolved from the AppHost directory.
// Default: .bitwarden/{resourceName}.{environment}.json relative to the AppHost directory.
bitwarden.WithCacheFile($".bitwarden/secrets.{builder.Environment.EnvironmentName}.json");

// Optional: override the AppHost auth cache directory.
// The auth cache stores the Bitwarden SDK auth session between runs so the integration can reuse the
// session and avoid re-authenticating on every run.
// Override to share the cache across multiple AppHost projects, or to store it in a CI cache directory.
// Relative paths are resolved from the Aspire store directory.
// Default: .bitwarden relative to the Aspire store directory.
bitwarden.WithAuthCacheDirectory(".bitwarden");

// Add a secret to the project with the value of the generated secret parameter.
// Configure this value with `Parameters:secrets-demo-api-key`, or let Aspire prompt for it.
// The secret is created or updated on each run. Use `GetSecret` if you only want to read an existing secret.
var demoApiKeySecret = bitwarden.AddSecret("demo-api-key");
var demoDbPasswordSecret = bitwarden.AddSecret("demo-db-password");

// Register an API service that references the Bitwarden secret manager
// There are two ways to reference secrets from the Bitwarden secret manager in Aspire.
var api = builder.AddProject<CommunityToolkit_Aspire_Hosting_Bitwarden_SecretManager_ApiService>("api")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

// 1. Using the secret manager client in code, which allows you to retrieve secrets at runtime and
//    supports dynamic secret retrieval without redeploying the application when secrets change.
// (See ApiService/Program.cs for an example of retrieving secrets from the client in code.)
api.WithReference(bitwarden)
    .WaitForCompletion(bitwarden)
    .WithEnvironment("DEMO_API_KEY_SECRET_ID", demoApiKeySecret.AsSecretId())
    .WithEnvironment("DEMO_DB_PASSWORD_SECRET_ID", demoDbPasswordSecret.AsSecretId())
    // Recommended: supply a least-privilege read-only access token so the client does not receive the management token.
    // IMPORTANT: the client token must be granted read permissions to the Bitwarden project.
    // This cannot be automated: Bitwarden does not expose an API for granting project access to a service account.
    // You must grant the service account read access to the project manually in the Bitwarden web vault or CLI.
    // For a newly created project this must be done after the first AppHost run that creates the project.
    .WithBitwardenAccessToken(bitwarden, accessToken /* replace with least privilege token */);

// Optional: override the client auth cache directory (separate from the AppHost auth cache).
// The client auth cache stores the Bitwarden SDK auth session between restarts so the client can
// reuse the session and avoid re-authenticating on every start (which would hit Bitwarden rate limits).
if (builder.ExecutionContext.IsPublishMode)
{
    api.WithBitwardenAuthCacheDirectory(bitwarden, "/bitwarden/auth-cache");
}

compose.ConfigureComposeFile(root =>
{
    root.AddVolume(new Volume
    {
        Name = "bitwarden-auth-cache"
    });
});

api.PublishAsDockerComposeService((resource, service) =>
{
    service.AddVolume(new Volume
    {
        Name = "bitwarden-auth-cache",
        Type = "volume",
        Source = "bitwarden-auth-cache",
        Target = "/bitwarden/auth-cache"
    });
});

// 2. Using direct secret references in the project configuration, which injects the secret value as an environment variable at runtime.
//    This approach is simpler (no Bitwarden code in the application) but requires redeploying the application whenever the secret value changes.
var client = builder.AddContainer("client", "curlimages/curl")
    .WithReference(api)
    .WaitForCompletion(bitwarden)
    .WithEnvironment("DEMO_API_KEY", demoApiKeySecret)
    .WithEntrypoint("sh")
    .WithArgs("-c", "curl -sv $API_HTTP?apiKey=$DEMO_API_KEY");

builder.Build().Run();
