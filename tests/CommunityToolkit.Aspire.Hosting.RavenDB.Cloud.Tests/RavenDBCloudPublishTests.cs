using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Utils;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Raven.Client.Exceptions;
using Raven.Client.Exceptions.Security;
using Raven.Client.ServerWide.Operations.Certificates;

namespace CommunityToolkit.Aspire.Hosting.RavenDB.Cloud.Tests;

public class RavenDBCloudPublishTests
{
    private static readonly RavenDBCloudSpec s_spec =
        RavenDBCloudSpec.Development(RavenDBCloudProvider.Aws, "eu-central-1", "Dev10", 20, RavenDBCloudStorageType.SsdStandard, RavenDBCloudDiskLayout.RootOnly, "Stable72");

    private sealed class AppHost : IDisposable
    {
        public AppHost(string step)
        {
            Output = Directory.CreateTempSubdirectory(".ravendb-cloud-test");
            Builder = TestDistributedApplicationBuilder.Create(
                ["AppHost:Operation=publish", $"Pipeline:OutputPath={Output.FullName}", $"Pipeline:Step={step}"]);

            Builder.Configuration["Parameters:ravendb-cloud-api-key"] = "test-api-key";
            Builder.Services.AddSingleton<IRavenDBCloudApiClientFactory>(Api);
            Builder.Services.AddSingleton<IRavenDBServerAdministrationFactory>(Server);
            Builder.Services.AddSingleton<IDeploymentStateManager>(State);
            Builder.Services.AddSingleton<IRavenDBCloudEnvironment>(Environment);
            Builder.Services.AddLogging(logging => logging.AddProvider(Logs));
            Builder.AddDockerComposeEnvironment("compose");
            ApiKey = Builder.AddParameter("ravendb-cloud-api-key", secret: true);
        }

        public DirectoryInfo Output { get; }

        public IDistributedApplicationTestingBuilder Builder { get; }

        public IResourceBuilder<ParameterResource> ApiKey { get; }

        public FakeRavenDBCloudApi Api { get; } = new() { BusyPolls = 1 };

        public FakeRavenDBServer Server { get; } = new();

        public InMemoryDeploymentStateManager State { get; } = new();

        /// <summary>The process environment the deployment sees.</summary>
        public FakeEnvironment Environment { get; } = new();

        public CollectingLoggerProvider Logs { get; } = new();

        public TimeSpan ReachableTimeout { get; set; } = TimeSpan.FromMinutes(1);

        public string Log => string.Join("\n", Logs.Messages);

        public string Compose => File.ReadAllText(Path.Combine(Output.FullName, "docker-compose.yaml")).ReplaceLineEndings("\n");

        public string EnvFile => string.Join("\n", Output.GetFiles(".env*").Select(f => File.ReadAllText(f.FullName)));

        public async Task RunAsync()
        {
            foreach (var deployment in Builder.Resources.SelectMany(r => r.Annotations.OfType<RavenDBCloudDeployment>()))
            {
                deployment.PollInterval = TimeSpan.Zero;
                deployment.ChangeGracePeriod = TimeSpan.Zero;
                deployment.ReachableTimeout = ReachableTimeout;
            }

            using var app = Builder.Build();
            await app.RunAsync(TestContext.Current.CancellationToken);
        }

        public void Dispose()
        {
            Builder.Dispose();
            Output.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ComposePublishLeavesTheServerOutAndReferencesTheDeployTimeValues()
    {
        using var host = new AppHost("publish");
        var orders = host.Builder.AddRavenDB("ravendb")
            .PublishAsRavenDBCloud(host.ApiKey, "shop-prod", s_spec, o => o.WithAllowedIps("203.0.113.0/24"))
            .AddDatabase("orders", ensureCreated: true);
        host.Builder.AddContainer("api", "busybox").WithReference(orders).WaitFor(orders);

        await host.RunAsync();

        var compose = host.Compose;
        Assert.DoesNotContain("ravendb/ravendb", compose);
        Assert.DoesNotContain("depends_on", compose);
        Assert.Contains("ConnectionStrings__orders: \"URL=${RAVENDB_URL};Database=orders\"", compose);
        Assert.Contains("Aspire__RavenDB__Client__orders__CertificateBase64: \"${RAVENDB_API_CERTIFICATE}\"", compose);
        Assert.Contains("Aspire__RavenDB__Client__CertificateBase64: \"${RAVENDB_API_CERTIFICATE}\"", compose);

        // aspire publish never calls the Cloud API: the values are placeholders.
        Assert.Contains("RAVENDB_URL=", host.EnvFile);
        Assert.Empty(host.Api.Requests);
    }

    [Fact]
    public async Task DeployFillsTheUrlAndEachApplicationsCertificateIntoTheEnvironment()
    {
        using var host = new AppHost("prepare-compose");
        var orders = host.Builder.AddRavenDB("ravendb")
            .PublishAsRavenDBCloud(host.ApiKey, "shop-prod", s_spec, o => o.WithAllowedIps("203.0.113.0/24"))
            .AddDatabase("orders", ensureCreated: true);
        host.Builder.AddContainer("api", "busybox").WithReference(orders);

        await host.RunAsync();

        var product = Assert.Single(host.Api.Products.Values);
        Assert.Equal("shop-prod", product.Name);
        Assert.Contains("orders", host.Server.Databases);

        var certificate = Assert.Single(host.Server.Certificates);
        Assert.Equal("aspire.api", certificate.Value.Name);
        Assert.Equal(DatabaseAccess.Admin, certificate.Value.Permissions["orders"]);

        var env = host.EnvFile;
        Assert.Contains("RAVENDB_URL=https://a.shop-prod.development.run", env);
        // The environment's own .env file carries the value; the published .env only names the variable.
        var value = env.Split('\n')
            .Where(l => l.StartsWith("RAVENDB_API_CERTIFICATE=", StringComparison.Ordinal))
            .Select(l => l["RAVENDB_API_CERTIFICATE=".Length..].Trim())
            .Single(v => v.Length > 0);
        Assert.Equal(certificate.Key, RavenDBCloudCertificates.GetThumbprint(Convert.FromBase64String(value)));
    }

    [Fact]
    public async Task BlockedPlanStopsTheDeploymentBeforeAnythingChanges()
    {
        using var host = new AppHost("prepare-compose");
        host.Api.AddProduct("shop-prod");
        var bigger = RavenDBCloudSpec.Development(RavenDBCloudProvider.Aws, "eu-central-1", "Dev20", 32, RavenDBCloudStorageType.SsdStandard, RavenDBCloudDiskLayout.RootOnly, "Stable72");
        var orders = host.Builder.AddRavenDB("ravendb")
            .PublishAsRavenDBCloud(host.ApiKey, "shop-prod", bigger, o => o.WithAllowedIps("203.0.113.0/24"))
            .AddDatabase("orders");
        host.Builder.AddContainer("api", "busybox").WithReference(orders);

        await host.RunAsync();

        Assert.Empty(host.Api.ChangeRequests);
        Assert.Empty(host.Server.Certificates);
        Assert.DoesNotContain("RAVENDB_URL=https", host.EnvFile, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApprovedChangesAreApplied()
    {
        using var host = new AppHost("prepare-compose");
        host.Environment.Variables["RavenDB__Cloud__ApproveChanges"] = "true";
        host.Api.AddProduct("shop-prod");
        var bigger = RavenDBCloudSpec.Development(RavenDBCloudProvider.Aws, "eu-central-1", "Dev20", 32, RavenDBCloudStorageType.SsdStandard, RavenDBCloudDiskLayout.RootOnly, "Stable72");
        var orders = host.Builder.AddRavenDB("ravendb")
            .PublishAsRavenDBCloud(host.ApiKey, "shop-prod", bigger, o => o.WithAllowedIps("203.0.113.0/24"))
            .AddDatabase("orders");
        host.Builder.AddContainer("api", "busybox").WithReference(orders);

        await host.RunAsync();

        var product = Assert.Single(host.Api.Products.Values);
        Assert.Equal(("Dev20", 32), (product.InstanceType, product.DiskSize));
    }

    /// <summary>A committed file must not approve every future change: only the process environment does.</summary>
    [Fact]
    public async Task ApprovalInConfigurationIsIgnored()
    {
        using var host = new AppHost("prepare-compose");
        host.Builder.Configuration["RavenDB:Cloud:ApproveChanges"] = "true";
        host.Api.AddProduct("shop-prod");
        var bigger = RavenDBCloudSpec.Development(RavenDBCloudProvider.Aws, "eu-central-1", "Dev20", 32, RavenDBCloudStorageType.SsdStandard, RavenDBCloudDiskLayout.RootOnly, "Stable72");
        var orders = host.Builder.AddRavenDB("ravendb")
            .PublishAsRavenDBCloud(host.ApiKey, "shop-prod", bigger, o => o.WithAllowedIps("203.0.113.0/24"))
            .AddDatabase("orders");
        host.Builder.AddContainer("api", "busybox").WithReference(orders);

        await host.RunAsync();

        Assert.Empty(host.Api.ChangeRequests);
    }

    [Fact]
    public async Task DeployWaitsUntilTheServerAcceptsConnections()
    {
        using var host = new AppHost("prepare-compose");
        // Right after a change the product reports Active a little before its node answers.
        host.Server.UnreachableFor = 2;
        var orders = host.Builder.AddRavenDB("ravendb")
            .PublishAsRavenDBCloud(host.ApiKey, "shop-prod", s_spec, o => o.WithAllowedIps("203.0.113.0/24"))
            .AddDatabase("orders", ensureCreated: true);
        host.Builder.AddContainer("api", "busybox").WithReference(orders);

        await host.RunAsync();

        Assert.Contains("orders", host.Server.Databases);
        Assert.Single(host.Server.Certificates);
        Assert.Contains("Waiting for RavenDB Cloud product 'shop-prod' at https://a.shop-prod.development.run to accept connections.", host.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServerThatNeverAnswersStopsTheDeploymentAndPointsAtTheAllowedIps()
    {
        using var host = new AppHost("prepare-compose") { ReachableTimeout = TimeSpan.Zero };
        host.Server.UnreachableFor = int.MaxValue;
        var orders = host.Builder.AddRavenDB("ravendb")
            .PublishAsRavenDBCloud(host.ApiKey, "shop-prod", s_spec, o => o.WithAllowedIps("203.0.113.0/24"))
            .AddDatabase("orders", ensureCreated: true);
        host.Builder.AddContainer("api", "busybox").WithReference(orders);

        await host.RunAsync();

        Assert.Empty(host.Server.Databases);
        Assert.Empty(host.Server.Certificates);
        Assert.Contains("did not answer for 00:00:00. Check that the address of this machine is among the product's allowed IPs.", host.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlanStopsWhenAnExistingProductDoesNotAnswer()
    {
        using var host = new AppHost("prepare-compose");
        host.Api.AddProduct("shop-prod");
        host.Server.UnreachableFor = int.MaxValue;
        var orders = host.Builder.AddRavenDB("ravendb")
            .PublishAsRavenDBCloud(host.ApiKey, "shop-prod", s_spec, o => o.WithAllowedIps("203.0.113.0/24"))
            .AddDatabase("orders");
        host.Builder.AddContainer("api", "busybox").WithReference(orders);

        await host.RunAsync();

        Assert.Empty(host.Server.Certificates);
        Assert.Contains(
            "unreachable: 'shop-prod' at https://a.shop-prod.development.run does not answer this machine. Add its address to the " +
            "product's allowed IPs in the RavenDB Cloud portal and deploy again.",
            host.Log,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task EveryConnectionUsesOneLiveAdminCertificate()
    {
        using var host = new AppHost("prepare-compose");
        host.Api.AddProduct("shop-prod");
        var orders = host.Builder.AddRavenDB("ravendb")
            .PublishAsRavenDBCloud(host.ApiKey, "shop-prod", s_spec, o => o.WithAllowedIps("203.0.113.0/24"))
            .AddDatabase("orders", ensureCreated: true);
        host.Builder.AddContainer("api", "busybox").WithReference(orders);

        await host.RunAsync();

        // The RavenDB client caches its HTTP clients per certificate thumbprint with the first instance it saw: a second
        // instance, or a disposed one, breaks the next TLS handshake once the product moves to another address.
        Assert.True(host.Server.ConnectionCertificates.Count >= 3);
        var certificate = Assert.Single(host.Server.ConnectionCertificates.Distinct());
        Assert.NotEmpty(certificate.Thumbprint);
    }

    [Fact]
    public void OnlyAServerThatDoesNotAnswerIsWaitedFor()
    {
        var timeout = new HttpRequestException("A connection attempt failed.", new SocketException(10060));
        var tlsRejected = new HttpRequestException("The SSL connection could not be established.", new System.Security.Authentication.AuthenticationException("The remote certificate was rejected."));

        Assert.True(RavenDBServerAdministration.IsUnreachable(timeout));
        Assert.True(RavenDBServerAdministration.IsUnreachable(new RavenException("An exception occurred while contacting the server.", timeout)));
        Assert.True(RavenDBServerAdministration.IsUnreachable(new AllTopologyNodesDownException("Failed to retrieve cluster topology.", new AggregateException(timeout))));

        Assert.False(RavenDBServerAdministration.IsUnreachable(new AllTopologyNodesDownException("Failed to retrieve cluster topology.", new AggregateException(tlsRejected))));
        Assert.False(RavenDBServerAdministration.IsUnreachable(new AuthorizationException("Could not authorize access to the server.")));
        Assert.False(RavenDBServerAdministration.IsUnreachable(new RavenException(
            "An exception occurred while contacting the server.",
            new HttpRequestException("The SSL connection could not be established.", new System.Security.Cryptography.CryptographicException("m_safeCertContext is an invalid handle.")))));
        Assert.False(RavenDBServerAdministration.IsUnreachable(new InvalidOperationException("Something else.")));
    }

    [Fact]
    public void RunModeKeepsTheLocalContainer()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("ravendb-cloud-api-key", secret: true);

        var server = builder.AddRavenDB("ravendb").PublishAsRavenDBCloud(apiKey, "shop-prod", s_spec);

        Assert.Empty(server.Resource.Annotations.OfType<RavenDBCloudDeployment>());
        Assert.Null(server.Resource.PublishStrategy);
    }

    [Fact]
    public void SecondPublishStrategyIsAnError()
    {
        using var host = new AppHost("publish");
        var server = host.Builder.AddRavenDB("ravendb").PublishAsRavenDBCloud(host.ApiKey, "shop-prod", s_spec);

        var exception = Assert.Throws<DistributedApplicationException>(() => server.PublishAsExistingRavenDBCloud(host.ApiKey, "shared", "shop"));

        Assert.Contains("already published with PublishAsRavenDBCloud, so it cannot also be published with PublishAsExistingRavenDBCloud", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OneProductForTwoServersIsAnError()
    {
        using var host = new AppHost("publish");
        host.Builder.AddRavenDB("a").PublishAsRavenDBCloud(host.ApiKey, "shop-prod", s_spec);
        host.Builder.AddRavenDB("b").PublishAsRavenDBCloud(host.ApiKey, "Shop-Prod", s_spec);

        var exception = await Assert.ThrowsAnyAsync<Exception>(host.RunAsync);

        Assert.Contains("are published to the same RavenDB Cloud product", exception.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("-billing")]
    [InlineData("billing.eu")]
    [InlineData("billing team")]
    public void InvalidClientNameIsRejected(string clientName)
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("ravendb-cloud-api-key", secret: true);

        Assert.Throws<ArgumentException>(() => builder.AddRavenDB("ravendb").PublishAsExistingRavenDBCloud(apiKey, "shared", clientName));
    }

    [Fact]
    public void ProductNameLongerThanRavenDBCloudAcceptsIsRejected()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("ravendb-cloud-api-key", secret: true);
        var name = new string('a', 101);

        Assert.Throws<ArgumentException>(() => builder.AddRavenDB("a").PublishAsRavenDBCloud(apiKey, name, s_spec));
        Assert.Throws<ArgumentException>(() => builder.AddRavenDB("b").PublishAsExistingRavenDBCloud(apiKey, name, "billing"));
    }

    [Fact]
    public void AttachedProductCannotBeTerminated()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("ravendb-cloud-api-key", secret: true);

        Assert.Throws<ArgumentException>(() => builder.AddRavenDB("ravendb").PublishAsExistingRavenDBCloud(apiKey, "shared", "billing", o => o.TerminateOnDestroy = true));
    }

    [Fact]
    public async Task ApiEndpointMustBeHttps()
    {
        using var host = new AppHost("prepare-compose");
        host.Environment.Variables["RAVENDB_CLOUD_ENDPOINT"] = "http://api.example.com";
        var orders = host.Builder.AddRavenDB("ravendb")
            .PublishAsRavenDBCloud(host.ApiKey, "shop-prod", s_spec, o => o.WithAllowedIps("203.0.113.0/24"))
            .AddDatabase("orders");
        host.Builder.AddContainer("api", "busybox").WithReference(orders);

        await host.RunAsync();

        // The API key never leaves for a plain HTTP endpoint.
        Assert.Empty(host.Api.Requests);
        Assert.Contains("The RavenDB Cloud API endpoint 'http://api.example.com' in RAVENDB_CLOUD_ENDPOINT must be an https URL", host.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OtherApiEndpointIsShownInThePlan()
    {
        using var host = new AppHost("prepare-compose");
        host.Environment.Variables["RAVENDB_CLOUD_ENDPOINT"] = "https://api.example.com";
        var orders = host.Builder.AddRavenDB("ravendb")
            .PublishAsRavenDBCloud(host.ApiKey, "shop-prod", s_spec, o => o.WithAllowedIps("203.0.113.0/24"))
            .AddDatabase("orders");
        host.Builder.AddContainer("api", "busybox").WithReference(orders);

        await host.RunAsync();

        Assert.Contains("endpoint: the RavenDB Cloud API at https://api.example.com, from RAVENDB_CLOUD_ENDPOINT, not the production one", host.Log, StringComparison.Ordinal);
    }

    [Fact]
    public void SpecFactoriesRejectWhatCannotBeAProduct()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RavenDBCloudSpec.Production(RavenDBCloudProvider.Aws, "eu-central-1", "P20", 0, RavenDBCloudStorageType.SsdStandard, RavenDBCloudDiskLayout.RootOnly, "Stable72"));
        Assert.Throws<ArgumentException>(() => RavenDBCloudSpec.Production(RavenDBCloudProvider.Aws, " ", "P20", 64, RavenDBCloudStorageType.SsdStandard, RavenDBCloudDiskLayout.RootOnly, "Stable72"));
        Assert.Throws<ArgumentException>(() => RavenDBCloudSpec.Development(RavenDBCloudProvider.Aws, "eu-central-1", "Dev10", 20, RavenDBCloudStorageType.SsdStandard, RavenDBCloudDiskLayout.RootOnly, ""));
        Assert.Throws<ArgumentNullException>(() => RavenDBCloudSpec.Development(RavenDBCloudProvider.Aws, null!, "Dev10", 20, RavenDBCloudStorageType.SsdStandard, RavenDBCloudDiskLayout.RootOnly, "Stable72"));
    }
}
