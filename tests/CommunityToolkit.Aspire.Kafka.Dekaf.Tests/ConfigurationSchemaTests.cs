// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Json.Schema;

namespace CommunityToolkit.Aspire.Kafka.Dekaf.Tests;

public class ConfigurationSchemaTests
{
    [Theory]
    [InlineData("Producer", true)]
    [InlineData("Producer", false)]
    [InlineData("Client", true)]
    [InlineData("Client", false)]
    [InlineData("Consumer", true)]
    [InlineData("Consumer", false)]
    [InlineData("ShareConsumer", true)]
    [InlineData("ShareConsumer", false)]
    [InlineData("AdminClient", true)]
    [InlineData("AdminClient", false)]
    [InlineData("SchemaRegistry", true)]
    [InlineData("SchemaRegistry", false)]
    public void SchemaValidatesAspireSettings(string role, bool valid)
    {
        var schema = JsonSchema.FromFile(Path.Combine(AppContext.BaseDirectory, "ConfigurationSchema.json"),
            new BuildOptions { Dialect = Dialect.Draft07, SchemaRegistry = new SchemaRegistry() });
        using var configuration = JsonDocument.Parse($$"""
            {
              "Aspire": {
                "Kafka": {
                  "Dekaf": {
                    "{{role}}": {
                      "ConnectionString": "localhost:9092",
                      "DisableHealthChecks": {{(valid ? "false" : "123")}},
                      "Config": { "ClientId": "schema-test" },
                      "HealthCheck": { "Timeout": "00:00:02" }
                    }
                  }
                }
              }
            }
            """);

        Assert.Equal(valid, schema.Evaluate(configuration.RootElement).IsValid);
    }

    [Theory]
    [InlineData("Producer")]
    [InlineData("Consumer")]
    [InlineData("AdminClient")]
    [InlineData("Client")]
    public void SchemaValidatesHealthCheckTimeout(string role)
    {
        AssertHealthCheckValid(role, """{ "Timeout": "00:00:02" }""", true);
        AssertHealthCheckValid(role, """{ "Timeout": "invalid" }""", false);
        AssertHealthCheckValid(role, """{ "Timeout": 2 }""", false);
        AssertHealthCheckValid(role, "false", false);
    }

    [Theory]
    [InlineData("""{ "DegradedThreshold": 500, "UnhealthyThreshold": 5000 }""", true)]
    [InlineData("""{ "DegradedThreshold": "invalid" }""", false)]
    [InlineData("""{ "UnhealthyThreshold": 1.5 }""", false)]
    [InlineData("""{ "NoAssignmentStatus": "Healthy" }""", true)]
    [InlineData("""{ "NoAssignmentStatus": "Degraded" }""", true)]
    [InlineData("""{ "NoAssignmentStatus": "Unhealthy" }""", true)]
    [InlineData("""{ "NoAssignmentStatus": "Unknown" }""", false)]
    public void SchemaValidatesConsumerHealthCheckOptions(string healthCheck, bool valid)
    {
        AssertHealthCheckValid("Consumer", healthCheck, valid);
    }

    private static void AssertHealthCheckValid(string role, string healthCheck, bool valid)
    {
        var schema = JsonSchema.FromFile(Path.Combine(AppContext.BaseDirectory, "ConfigurationSchema.json"),
            new BuildOptions { Dialect = Dialect.Draft07, SchemaRegistry = new SchemaRegistry() });
        using var configuration = JsonDocument.Parse($$"""
            {
              "Aspire": {
                "Kafka": {
                  "Dekaf": {
                    "{{role}}": {
                      "HealthCheck": {{healthCheck}}
                    }
                  }
                }
              }
            }
            """);

        Assert.Equal(valid, schema.Evaluate(configuration.RootElement).IsValid);
    }
}
