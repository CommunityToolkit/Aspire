using Projects;

var builder = DistributedApplication.CreateBuilder(args);

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

// Optionally share the authentication cache between all applications that reference this instance.
bitwarden.WithAuthStateFile("obj/auth.cache");

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

// 2. Using direct secret references in the project configuration, which injects the secret value as an environment variable at runtime.
//    This approach is simpler (no Bitwarden code in the application) but requires redeploying the application whenever the secret value changes.
api.WithBitwardenSecretValue("DEMO_API_KEY", demoApiKeySecret.Resource);

if (OperatingSystem.IsLinux())
{
    // Work around Linux trust-store discovery issues in Bitwarden.Secrets.Sdk 1.0.0.
    api.WithEnvironment("SSL_CERT_FILE", "/etc/ssl/certs/ca-certificates.crt")
        .WithEnvironment("SSL_CERT_DIR", "/etc/ssl/certs");
}

builder.Build().Run();