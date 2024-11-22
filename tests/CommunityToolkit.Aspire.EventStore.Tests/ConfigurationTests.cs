// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CommunityToolkit.Aspire.EventStore.Tests;

public class ConfigurationTests
{
    [Fact]
    public void ConnectionStringIsNullByDefault() =>
        Assert.Null(new EventStoreSettings().ConnectionString);

    [Fact]
    public void HealthChecksEnabledByDefault() =>
        Assert.False(new EventStoreSettings().DisableHealthChecks);

    [Fact]
    public void DisableTracingIsFalseByDefault() =>
      Assert.False(new EventStoreSettings().DisableTracing);
}
