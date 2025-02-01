// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CommunityToolkit.Aspire.GoFeatureFlag.Tests;

public class ConfigurationTests
{
    [Fact]
    public void EndpointIsNullByDefault() =>
        Assert.Null(new GoFeatureFlagClientSettings().Endpoint);

    [Fact]
    public void HealthChecksEnabledByDefault() =>
        Assert.False(new GoFeatureFlagClientSettings().DisableHealthChecks);
}
