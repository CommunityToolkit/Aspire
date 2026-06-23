using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager.Tests;

public class BitwardenSecretManagerPublishingTests
{
    [Fact]
    public void AddSecret_InPublishMode_DeclaresGraphButExcludesManagedSecretFromManifest()
    {
        using var appBuilder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";
        appBuilder.Configuration["Parameters:bitwarden-organization-id"] = Guid.NewGuid().ToString("D");
        appBuilder.Configuration["Parameters:bitwarden-project"] = "managed-project";

        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
        var organizationId = appBuilder.AddParameter("bitwarden-organization-id");
        var projectParam = appBuilder.AddParameter("bitwarden-project");

        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationId, accessToken);
        var managedSecret = bitwarden.AddSecret("api-key");

        using var app = appBuilder.Build();

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var secretResource = Assert.Single(model.Resources.OfType<BitwardenSecretResource>());

        Assert.Same(managedSecret.Resource, secretResource);
        Assert.Single(bitwarden.Resource.DeclaredSecretReferences);
        Assert.Same(managedSecret.Resource, bitwarden.Resource.DeclaredSecretReferences.Single());
        Assert.Contains(ManifestPublishingCallbackAnnotation.Ignore, secretResource.Annotations);
    }
}