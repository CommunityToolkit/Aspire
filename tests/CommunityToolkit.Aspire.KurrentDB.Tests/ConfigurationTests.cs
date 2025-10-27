// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CommunityToolkit.Aspire.KurrentDB.Tests;

public class ConfigurationTests
{
    [Fact]
    public void ConnectionStringIsNullByDefault() =>
        Assert.Null(new KurrentDBSettings().ConnectionString);

    [Fact]
    public void HealthChecksEnabledByDefault() =>
        Assert.False(new KurrentDBSettings().DisableHealthChecks);

    [Fact]
    public void HealthCheckTimeoutNullByDefault() =>
     Assert.Null(new KurrentDBSettings().HealthCheckTimeout);

    [Fact]
    public void DisableTracingIsFalseByDefault() =>
      Assert.False(new KurrentDBSettings().DisableTracing);
}
