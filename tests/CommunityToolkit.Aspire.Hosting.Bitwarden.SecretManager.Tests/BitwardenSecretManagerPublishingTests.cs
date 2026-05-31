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

        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);

        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "managed-project", Guid.NewGuid(), accessToken);
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