// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;

namespace CommunityToolkit.Aspire.SurrealDb.Tests;

public class ConfigurationTests
{
    [Fact]
    public void HealthChecksEnabledByDefault()
    {
        new SurrealDbClientSettings().DisableHealthChecks.Should().BeFalse();
    }

    [Fact]
    public void OptionsAreNullByDefault()
    {
        new SurrealDbClientSettings().Options.Should().BeNull();
    }
    
    [Fact]
    public void LifetimeIsSingletonByDefault()
    {
        new SurrealDbClientSettings().Lifetime.Should().Be(ServiceLifetime.Singleton);
    }
}
