// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace CommunityToolkit.Aspire.Marten.Tests;

public class ConfigurationTests
{
    [Fact]
    public void ConnectionStringIsNullByDefault() =>
        Assert.Null(new MartenSettings().ConnectionString);

    [Fact]
    public void HealthChecksEnabledByDefault() =>
        Assert.False(new MartenSettings().DisableHealthChecks);

    [Fact]
    public void HealthCheckTimeoutIsNullByDefault() =>
      Assert.Null(new MartenSettings().HealthCheckTimeout);

    [Fact]
    public void TracingEnabledByDefault() =>
        Assert.False(new MartenSettings().DisableTracing);

    [Fact]
    public void MetricsEnabledByDefault() =>
        Assert.False(new MartenSettings().DisableMetrics);
}
