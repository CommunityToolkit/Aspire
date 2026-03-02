using Aspire.Components.Common.Tests;
using Aspire.Components.ConformanceTests;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Minio;
using Minio.DataModel.Args;

namespace CommunityToolkit.Aspire.Minio.Client.Tests;

public class ConformanceTests(MinioContainerFeature minioContainerFeature) : ConformanceTests<IMinioClient, MinioClientSettings>, IClassFixture<MinioContainerFeature>
{
    protected override ServiceLifetime ServiceLifetime => ServiceLifetime.Singleton;

    protected override string ActivitySourceName => string.Empty;

    protected override string[] RequiredLogCategories => [];

    protected override bool CanConnectToServer => RequiresDockerAttribute.IsSupported;

    protected override bool SupportsKeyedRegistrations => false;

    protected override string ConfigurationSectionName => "Aspire:Minio:Client";

    protected override void PopulateConfiguration(ConfigurationManager configuration, string? key = null)
    {
        var endpoint = RequiresDockerAttribute.IsSupported ?
          $"{minioContainerFeature.GetContainerEndpoint()}" :
          "Endpoint=http://localhost:9000";

        const string accessKey = "minioadmin";
        const string secretKey = "minioadmin";
        
        var connString = $"{endpoint};AccessKey={accessKey}; SecretKey={secretKey}";
        
        configuration.AddInMemoryCollection(
          [
              new KeyValuePair<string, string?>(CreateConfigKey(ConfigurationSectionName, null, suffix: "Endpoint"), endpoint),
              new KeyValuePair<string, string?>(CreateConfigKey(ConfigurationSectionName+":Credentials", null, suffix: "AccessKey"), accessKey),
              new KeyValuePair<string, string?>(CreateConfigKey(ConfigurationSectionName+":Credentials", null, suffix: "SecretKey"), secretKey),
              new KeyValuePair<string, string?>($"ConnectionStrings:{key ?? "minio"}", connString)
          ]);
    }

    protected override void RegisterComponent(HostApplicationBuilder builder, Action<MinioClientSettings>? configure = null, string? key = null)
    {
        builder.AddMinioClient("minio", configureSettings: configure);
    }

    protected override void SetHealthCheck(MinioClientSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void SetMetrics(MinioClientSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void SetTracing(MinioClientSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void TriggerActivity(IMinioClient service)
    {
        using var source = new CancellationTokenSource(100);
        
        if (service is MinioClient minioClient)
        {
            minioClient.ListBucketsAsync(source.Token).Wait();
        }
    }

    protected override string ValidJsonConfig => """
                                                 {
                                                   "Aspire": {
                                                     "Minio": {
                                                        "Client": {
                                                            "Endpoint": "http://localhost:9001",
                                                            "Credentials": {
                                                                "AccessKey": "minioadmin",
                                                                "SecretKey": "minioadmin",
                                                            }
                                                        }
                                                     }
                                                   }
                                                 }
                                                 """;

}