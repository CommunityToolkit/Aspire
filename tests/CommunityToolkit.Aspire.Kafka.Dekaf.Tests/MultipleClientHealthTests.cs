// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CommunityToolkit.Aspire.Kafka.Dekaf.Tests;

public class MultipleClientHealthTests
{
    [Theory]
    [InlineData("Producer", false)]
    [InlineData("Producer", true)]
    [InlineData("Consumer", false)]
    [InlineData("Consumer", true)]
    [InlineData("ShareConsumer", false)]
    [InlineData("ShareConsumer", true)]
    public async Task DifferentMessageTypesHaveIndependentHealthChecks(string role, bool keyed)
    {
        var builder = ClientTestHelpers.CreateBuilder();
        Register<string, string>(builder, role, keyed);
        Register<int, byte[]>(builder, role, keyed);
        await using var services = builder.Services.BuildServiceProvider();
        var registrations = services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        Assert.Equal(2, registrations.Count);
        Assert.Equal(2, registrations.Select(registration => registration.Name).Distinct().Count());
        var healthCheckTypes = registrations.Select(registration => registration.Factory(services).GetType()).ToArray();
        Assert.Contains(healthCheckTypes, type => type.GenericTypeArguments.SequenceEqual([typeof(string), typeof(string)]));
        Assert.Contains(healthCheckTypes, type => type.GenericTypeArguments.SequenceEqual([typeof(int), typeof(byte[])]));

        var report = await services.GetRequiredService<HealthCheckService>().CheckHealthAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, report.Entries.Count);
        Assert.All(report.Entries.Values, entry => Assert.Equal(HealthStatus.Unhealthy, entry.Status));
    }

    private static void Register<TKey, TValue>(HostApplicationBuilder builder, string role, bool keyed)
    {
        switch (role, keyed)
        {
            case ("Producer", false):
                builder.AddDekafKafkaProducer<TKey, TValue>("messaging");
                break;
            case ("Producer", true):
                builder.AddKeyedDekafKafkaProducer<TKey, TValue>("messaging");
                break;
            case ("Consumer", false):
                builder.AddDekafKafkaConsumer<TKey, TValue>("messaging");
                break;
            case ("Consumer", true):
                builder.AddKeyedDekafKafkaConsumer<TKey, TValue>("messaging");
                break;
            case ("ShareConsumer", false):
                builder.AddDekafKafkaShareConsumer<TKey, TValue>("messaging");
                break;
            case ("ShareConsumer", true):
                builder.AddKeyedDekafKafkaShareConsumer<TKey, TValue>("messaging");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(role));
        }
    }
}
