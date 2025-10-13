// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Neon.Tests;

[RequiresDocker]
public class NeonPublicApiTests : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Neon_AppHost>>
{
    private readonly AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Neon_AppHost> _fixture;

    public NeonPublicApiTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Neon_AppHost> fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [RequiresDocker]
    public async Task NeonResourceGetsAddedToManifest()
    {
        var manifest = _fixture.App.Services.GetManifest();
        var neonResource = manifest["neon"];
        Assert.NotNull(neonResource);
    }
}
