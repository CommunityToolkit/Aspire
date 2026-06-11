using Amazon.S3;
using Aspire.Components.ConformanceTests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.SeaweedFS.Client.Tests;

/// <summary>
/// Conformance tests for the SeaweedFS S3 Client integration.
/// Ensures the component adheres to the strict architectural guidelines of .NET Aspire.
/// </summary>
public class SeaweedFSClientConformanceTests : ConformanceTests<IAmazonS3, SeaweedFSClientSettings>
{
    protected override ServiceLifetime ServiceLifetime => ServiceLifetime.Singleton;

    protected override string ActivitySourceName => string.Empty;

    protected override string[] RequiredLogCategories => [];

    protected override bool CanConnectToServer => false;

    protected override bool SupportsKeyedRegistrations => true;

    protected override string ConfigurationSectionName => "Aspire:SeaweedFS:Client";

    protected override void PopulateConfiguration(ConfigurationManager configuration, string? key = null)
    {
        string endpoint = "http://localhost:8333";
        const string accessKey = "admin";
        const string secretKey = "admin-secret";

        string connString = $"Endpoint={endpoint};AccessKey={accessKey};SecretKey={secretKey};UseSsl=false";

        configuration.AddInMemoryCollection(
        [
            new KeyValuePair<string, string?>(CreateConfigKey(ConfigurationSectionName, key, suffix: "Endpoint"), endpoint),
            new KeyValuePair<string, string?>(CreateConfigKey(ConfigurationSectionName, key, suffix: "AccessKey"), accessKey),
            new KeyValuePair<string, string?>(CreateConfigKey(ConfigurationSectionName, key, suffix: "SecretKey"), secretKey),
            new KeyValuePair<string, string?>(CreateConfigKey(ConfigurationSectionName, key, suffix: "UseSsl"), "false"),
            new KeyValuePair<string, string?>($"ConnectionStrings:{key ?? "seaweedfs"}", connString)
        ]);
    }

    protected override void RegisterComponent(HostApplicationBuilder builder, Action<SeaweedFSClientSettings>? configure = null, string? key = null)
    {
        if (key is null)
        {
            builder.AddSeaweedFSS3Client("seaweedfs", configure);
        }
        else
        {
            builder.AddSeaweedFSS3Client(key, configure);
        }
    }

    protected override void SetHealthCheck(SeaweedFSClientSettings options, bool enabled)
    {
        options.DisableHealthChecks = !enabled;
    }

    protected override void SetMetrics(SeaweedFSClientSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void SetTracing(SeaweedFSClientSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void TriggerActivity(IAmazonS3 service)
    {
        using CancellationTokenSource source = new(TimeSpan.FromMilliseconds(100));
        try
        {
            service.ListBucketsAsync(source.Token).Wait();
        }
        catch
        {
        }
    }

    protected override string ValidJsonConfig => """
                                                 {
                                                   "Aspire": {
                                                     "SeaweedFS": {
                                                        "Client": {
                                                            "Endpoint": "http://localhost:8333",
                                                            "AccessKey": "admin",
                                                            "SecretKey": "admin-secret",
                                                            "ForcePathStyle": true,
                                                            "UseSsl": false,
                                                            "DisableHealthChecks": false
                                                        }
                                                     }
                                                   }
                                                 }
                                                 """;
}