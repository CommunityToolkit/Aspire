// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CommunityToolkit.Aspire.SurrealDb.Tests;

public class ConfigurationTests
{
    [Fact]
    public void HealthChecksEnabledByDefault() =>
        Assert.False(new SurrealDbClientSettings().DisableHealthChecks);

    [Fact]
    public void OptionsAreNullByDefault() =>
        Assert.Null(new SurrealDbClientSettings().Options);

    [Fact]
    public void LifetimeIsSingletonByDefault() =>
        Assert.Equal(ServiceLifetime.Singleton, new SurrealDbClientSettings().Lifetime);
}
