// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
#pragma warning disable ASPIREUSERSECRETS001

using System.Security.Cryptography;
using System.Text;

namespace CommunityToolkit.Aspire.Hosting.GlitchTip;

internal static class GlitchTipLocalHosting
{
    internal static void Configure(IResourceBuilder<GlitchTipResource> project)
    {
        var builder = project.ApplicationBuilder;
        var name = project.Resource.Name;
        var scope = GetStorageScope(builder.AppHostDirectory);
        var secretKey = builder.AddParameter($"{name}-{scope}-secret-key", new GlitchTipLocalSecretDefault(builder.UserSecretsManager, $"{name}-{scope}-secret-key", 64), secret: true);
        var adminEmail = builder.AddParameter($"{name}-admin-email", "admin@example.com");
        var adminPassword = builder.AddParameter($"{name}-{scope}-admin-password", new GlitchTipLocalSecretDefault(builder.UserSecretsManager, $"{name}-{scope}-admin-password", 32), secret: true);
        var organization = builder.AddParameter($"{name}-local-organization", "aspire");
        var team = builder.AddParameter($"{name}-local-team", "aspire");
        project.Resource.AdminEmail = adminEmail.Resource;
        project.Resource.AdminPassword = adminPassword.Resource;
        project.Resource.Organization = organization.Resource;
        project.Resource.InitialTeam = team.Resource;

        var postgresPassword = builder.AddParameter($"{name}-{scope}-postgres-password", new GlitchTipLocalSecretDefault(builder.UserSecretsManager, $"{name}-{scope}-postgres-password", 32), secret: true);
        var postgres = builder.AddPostgres($"{name}-postgres", password: postgresPassword)
            .WithImageTag("18.3")
            .WithDataVolume($"{name}-{scope}-postgres")
            .ExcludeFromManifest();
        var data = postgres.AddDatabase($"{name}-database", "glitchtip");
        var server = builder.AddContainer($"{name}-server", "glitchtip/glitchtip", "6.2.6")
            .WithHttpEndpoint(targetPort: 8000, name: "http")
            .WithEnvironment("DATABASE_URL", ReferenceExpression.Create($"postgres://{postgres.Resource.UserNameReference}:{postgres.Resource.PasswordParameter:uri}@{postgres.Resource.PrimaryEndpoint.Property(EndpointProperty.Host)}:{postgres.Resource.PrimaryEndpoint.Property(EndpointProperty.Port)}/{data.Resource.DatabaseName}"))
            // An empty URL selects the supported PostgreSQL cache and task broker.
            .WithEnvironment("VALKEY_URL", "")
            .WithEnvironment("SECRET_KEY", secretKey)
            .WithEnvironment("EMAIL_URL", "consolemail://")
            .WithEnvironment("DEFAULT_FROM_EMAIL", "glitchtip@example.com")
            .WithEnvironment("SERVER_ROLE", "all_in_one")
            .WithCertificateTrustScope(CertificateTrustScope.System)
            .WithEnvironment("GLITCHTIP_ENABLE_MCP", "True")
            .WithEnvironment("GLITCHTIP_ENABLE_DUCKDB", "True")
            .WithEnvironment("GLITCHTIP_COLD_STORAGE_DIR", "/code/uploads/cold-storage")
            .WithEnvironment("ENABLE_ORGANIZATION_CREATION", "True")
            .WithEnvironment("GLITCHTIP_UPTIME_ALLOW_PRIVATE_IPS", "True")
            .WithVolume($"{name}-{scope}-uploads", "/code/uploads")
            .WaitFor(postgres)
            .WithHttpHealthCheck("/_health/")
            .ExcludeFromManifest();
        // MCP OAuth accepts HTTP only for loopback issuers. Advertise the host-facing URL,
        // while reporting DSNs resolve separately for each consumer network context.
        server.WithEnvironment(context => context.EnvironmentVariables["GLITCHTIP_URL"] = server.GetEndpoint("http").Url);
        server.WithUrlForEndpoint("http", url => url.DisplayText = "GlitchTip");
        server.WithUrlForEndpoint("http", endpoint => new ResourceUrlAnnotation { Url = "/mcp", DisplayText = "MCP (authenticate in your client)" });
        project.Resource.LocalServer = server.Resource;
        project.WithRelationship(server.Resource, "Project on");
        builder.Eventing.Subscribe<ResourceReadyEvent>(server.Resource, async (evt, cancellationToken) =>
        {
            await GlitchTipLifecycle.ProvisionLocalAsync(project, evt.Services, cancellationToken).ConfigureAwait(false);
        });
    }

    internal static string GetStorageScope(string appHostDirectory)
    {
        var path = Path.GetFullPath(appHostDirectory).TrimEnd(Path.DirectorySeparatorChar);
        if (OperatingSystem.IsWindows()) path = path.ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)))[..12].ToLowerInvariant();
    }
}