using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.Logging;
using Raven.Client.ServerWide.Operations.Certificates;

namespace CommunityToolkit.Aspire.Hosting.RavenDB.Cloud.Tests;

/// <summary>
/// An in-memory RavenDB Cloud API with the endpoints the integration calls, shaped after
/// https://api.cloud.ravendb.net/api/v1/swagger.json.
/// </summary>
internal sealed class FakeRavenDBCloudApi : HttpMessageHandler, IRavenDBCloudApiClientFactory
{
    private int _nextId;

    public ConcurrentDictionary<string, FakeProduct> Products { get; } = new();

    public List<JsonObject> CreateRequests { get; } = [];

    public List<JsonObject> ChangeRequests { get; } = [];

    public List<string> TerminatedProductIds { get; } = [];

    public List<string> Requests { get; } = [];

    /// <summary>The client admin certificate every product hands out.</summary>
    public byte[] AdminCertificate { get; } = TestCertificates.CreatePfx("admin");

    /// <summary>How many status checks a product reports "Creating" or "DeployingChanges" before it becomes "Active".</summary>
    public int BusyPolls { get; set; } = 2;

    /// <summary>Answers the next requests with these statuses before handling them, as a busy or failing API does.</summary>
    public ConcurrentQueue<HttpStatusCode> Failures { get; } = new();

    public FakeProduct AddProduct(string name, string status = "Active", string tier = "Development", string instanceType = "Dev10")
    {
        var id = $"product-{Interlocked.Increment(ref _nextId)}";

        // Host names without a scheme, as the real API reports them.
        var product = new FakeProduct(id, name)
        {
            Status = status,
            Tier = tier,
            InstanceType = instanceType,
            Subdomain = RavenDBCloudDeployment.DeriveSubdomain(name),
            Dns = [$"a.{name}.development.run"],
        };
        Products[id] = product;
        return product;
    }

    public RavenDBCloudApiClient Create(string endpoint, string apiKey) =>
        new(new HttpClient(this, disposeHandler: false), endpoint, apiKey) { RetryDelay = TimeSpan.FromMilliseconds(1) };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // As the real API's gateway does.
        if (request.Headers.UserAgent.Count == 0)
        {
            return new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("<html>403 Forbidden</html>") };
        }

        var path = request.RequestUri!.AbsolutePath;

        lock (Requests)
        {
            Requests.Add($"{request.Method} {path}");
        }

        if (Failures.TryDequeue(out var failure))
        {
            return new HttpResponseMessage(failure);
        }

        if (request.Method == HttpMethod.Get && path == "/api/v1/products/list")
        {
            // Like the real API, the list leaves out the products hidden in the portal.
            return Json(new { items = Products.Values.Where(p => !p.Hidden).Select(p => new { id = p.Id, name = p.Name }) });
        }

        if (request.Method == HttpMethod.Get && path.StartsWith("/api/v1/products/details/", StringComparison.Ordinal))
        {
            if (!Products.TryGetValue(path.Split('/').Last(), out var product))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (product.Status is "Creating" or "DeployingChanges" && ++product.Polls >= BusyPolls)
            {
                product.Status = "Active";
                product.Polls = 0;
            }

            return Json(new
            {
                id = product.Id,
                displayName = product.Name,
                subdomainName = product.Subdomain,
                status = product.Status,
                tier = product.Tier,
                cloudProvider = product.Provider,
                region = product.Region,
                instanceType = product.InstanceType,
                releaseChannel = product.ReleaseChannel,
                // A product being created has no URL yet.
                dns = product.Status == "Creating" ? [] : product.Dns,
                nodeTags = product.NodeTags,
                hardwareInfo = product.HasDataDisk
                    ? new
                    {
                        storage = new { size = 30, iops = (int?)3000, throughput = (double?)125, type = "SsdStandard" },
                        additionalStorage = new { size = product.DiskSize, iops = product.Iops, throughput = product.Throughput, type = product.StorageType },
                    }
                    : (object)new { storage = new { size = product.DiskSize, iops = product.Iops, throughput = product.Throughput, type = product.StorageType } },
                security = new { allowedIps = product.AllowedIps },
            });
        }

        if (request.Method == HttpMethod.Post && path == "/api/v1/products/create")
        {
            var body = await ReadAsync(request, cancellationToken);

            lock (CreateRequests)
            {
                CreateRequests.Add(body);
            }

            // As the real API validates it: no free products, a disk layout, and a subdomain unique in the account.
            if (body["tier"]?.GetValue<string>() == "Free")
            {
                return Json(new { tier = new[] { "Creating Free product via API is not allowed." } }, HttpStatusCode.BadRequest);
            }

            if (body["diskLayout"]?.GetValue<string>() is not ("RootOnly" or "SingleDataDisk"))
            {
                return Json(new { diskLayout = new[] { "DiskLayout is required." } }, HttpStatusCode.BadRequest);
            }

            var subdomain = body["subdomainName"]?.GetValue<string>();

            if (string.IsNullOrEmpty(subdomain))
            {
                return Json(new { subdomainName = new[] { "Subdomain Name is required." } }, HttpStatusCode.BadRequest);
            }

            // Names are unique among the products the portal shows, terminated ones included.
            var name = body["displayName"]!.GetValue<string>();

            if (Products.Values.Any(p => !p.Hidden && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return Json(new { displayName = new[] { "Provided Display Name is already taken." } }, HttpStatusCode.BadRequest);
            }

            var product = AddProduct(body["displayName"]!.GetValue<string>(), "Creating", body["tier"]!.GetValue<string>(), body["instanceTypeName"]!.GetValue<string>());
            product.Subdomain = subdomain;
            product.HasDataDisk = body["diskLayout"]!.GetValue<string>() == "SingleDataDisk";
            product.Provider = body["cloudProvider"]!.GetValue<string>();
            product.Region = body["region"]!.GetValue<string>();
            product.ReleaseChannel = body["releaseChannel"]?.GetValue<string>();
            product.DiskSize = body["diskSize"]!.GetValue<int>();
            product.StorageType = body["storageTypeName"]!.GetValue<string>();
            product.AllowedIps = [.. body["allowedIps"]!.AsArray().Select(n => n!.GetValue<string>())];
            return Json(new { productId = product.Id }, HttpStatusCode.Accepted);
        }

        if (request.Method == HttpMethod.Post && path == "/api/v1/products/instance-type")
        {
            var body = await ReadAsync(request, cancellationToken);
            ChangeRequests.Add(body);
            var product = Products[body["productId"]!.GetValue<string>()];
            product.InstanceType = body["instanceType"]!.GetValue<string>();
            product.Status = "DeployingChanges";
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }

        if (request.Method == HttpMethod.Post && path == "/api/v1/products/storage")
        {
            var body = await ReadAsync(request, cancellationToken);
            ChangeRequests.Add(body);
            var product = Products[body["productId"]!.GetValue<string>()];
            product.DiskSize = body["size"]!.GetValue<int>();
            product.StorageType = body["storageType"]!.GetValue<string>();
            product.Status = "DeployingChanges";
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }

        if (request.Method == HttpMethod.Post && path.StartsWith("/api/v1/products/terminate/", StringComparison.Ordinal))
        {
            var id = path.Split('/').Last();

            lock (TerminatedProductIds)
            {
                TerminatedProductIds.Add(id);
            }

            // Like the real API, a terminated product stays in the list unless it is hidden.
            if (!Products.TryGetValue(id, out var terminated))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            terminated.Status = "Terminated";
            terminated.Hidden = (await ReadAsync(request, cancellationToken))["hideInPortal"]?.GetValue<bool>() == true;
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }

        if (request.Method == HttpMethod.Get && path.StartsWith("/api/v1/metadata/regions/", StringComparison.Ordinal))
        {
            return path.EndsWith("/Aws", StringComparison.Ordinal)
                ? Json(new { names = new[] { "us-east-1", "eu-central-1" } })
                : Json(new { names = new[] { "westeurope", "northeurope" } });
        }

        if (request.Method == HttpMethod.Get && path.StartsWith("/api/v1/metadata/instance-types/", StringComparison.Ordinal))
        {
            var standard = new { name = "SsdStandard", iopsRange = (object?)null };
            var premium = new { name = "SsdPremium", iopsRange = (object?)new { min = 3000, max = 16000 } };

            return Json(new
            {
                instanceTypes = new object[]
                {
                    new { name = "Free", tier = "Free", parameters = new { virtualCpus = 2, ram = 0.5, numberOfNodes = 1, availableDiskSizes = new[] { 10 }, availableStorageTypes = new[] { standard } } },
                    new { name = "Dev10", tier = "Development", parameters = new { virtualCpus = 2, ram = 2, numberOfNodes = 1, availableDiskSizes = new[] { 10, 20, 32 }, availableStorageTypes = new object[] { standard, premium } } },
                    new { name = "Dev20", tier = "Development", parameters = new { virtualCpus = 2, ram = 4, numberOfNodes = 1, availableDiskSizes = new[] { 20, 32, 64 }, availableStorageTypes = new object[] { standard, premium } } },
                    new { name = "P20", tier = "Production", parameters = new { virtualCpus = 4, ram = 8, numberOfNodes = 3, availableDiskSizes = new[] { 64, 128 }, availableStorageTypes = new object[] { standard, premium } } },
                },
            });
        }

        if (request.Method == HttpMethod.Get && path.StartsWith("/api/v1/products/security/certificate/", StringComparison.Ordinal))
        {
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(TestCertificates.Bundle("admin", AdminCertificate)) };
        }

        if (request.Method == HttpMethod.Get && path == "/api/v1/metadata/release-channels")
        {
            return Json(new { defaultReleaseChannel = "Stable72", releaseChannels = new[] { new { name = "Stable72", ravenVersion = "7.2.7" }, new { name = "Stable62", ravenVersion = "6.2.20" } } });
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static async Task<JsonObject> ReadAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!.AsObject();

    private static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = JsonContent.Create(value, options: new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
}

internal sealed class FakeProduct(string id, string name)
{
    public string Id { get; } = id;

    public string Name { get; } = name;

    public string? Subdomain { get; set; }

    public string Status { get; set; } = "Active";

    public bool Hidden { get; set; }

    public string Tier { get; set; } = "Development";

    public string Provider { get; set; } = "Aws";

    public string Region { get; set; } = "eu-central-1";

    public string InstanceType { get; set; } = "Dev10";

    public string? ReleaseChannel { get; set; } = "Stable72";

    public int DiskSize { get; set; } = 20;

    public int? Iops { get; set; }

    public double? Throughput { get; set; }

    public string StorageType { get; set; } = "SsdStandard";

    /// <summary>
    /// A separate data disk, as a product set up in the portal can have: the disk properties above are then the data
    /// disk's, and the API reports a 30 GB system disk besides.
    /// </summary>
    public bool HasDataDisk { get; set; }

    public string[] AllowedIps { get; set; } = ["203.0.113.0/24"];

    public string[] Dns { get; set; } = [];

    public string[] NodeTags { get; set; } = ["A"];

    public int Polls { get; set; }
}

/// <summary>An in-memory RavenDB server with the certificate and database operations a deployment performs.</summary>
internal sealed class FakeRavenDBServer : IRavenDBServerAdministrationFactory
{
    public HashSet<string> Databases { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, FakeCertificate> Certificates { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> DeletedThumbprints { get; } = [];

    /// <summary>Fails the next deletions with this many exceptions, as an unreachable server does.</summary>
    public int FailingDeletes { get; set; }

    /// <summary>Fails this many pings, as a server that does not answer yet; <see cref="int.MaxValue"/> never answers.</summary>
    public int UnreachableFor { get; set; }

    /// <summary>Node URLs and admin certificate thumbprint of every connection.</summary>
    public List<(IReadOnlyList<string> Urls, string? CertificateThumbprint)> Connections { get; } = [];

    /// <summary>The admin certificate instance every connection was given.</summary>
    public List<X509Certificate2> ConnectionCertificates { get; } = [];

    /// <summary>Registers a certificate as if an earlier deployment or another client had issued it.</summary>
    public string Register(string name, params string[] databases) =>
        Register(name, SecurityClearance.ValidUser, databases.ToDictionary(d => d, _ => DatabaseAccess.Admin));

    public string Register(string name, SecurityClearance clearance, Dictionary<string, DatabaseAccess> permissions)
    {
        var thumbprint = RavenDBCloudCertificates.GetThumbprint(TestCertificates.CreatePfx(name));
        Certificates[thumbprint] = new FakeCertificate(name, permissions, clearance);
        return thumbprint;
    }

    public IRavenDBServerAdministration Create(IReadOnlyList<string> urls, X509Certificate2? certificate)
    {
        Connections.Add((urls, certificate?.Thumbprint));

        if (certificate is not null)
        {
            ConnectionCertificates.Add(certificate);
        }

        return new Session(this);
    }

    private sealed class Session(FakeRavenDBServer server) : IRavenDBServerAdministration
    {
        public Task PingAsync(CancellationToken cancellationToken)
        {
            if (server.UnreachableFor > 0)
            {
                server.UnreachableFor -= server.UnreachableFor == int.MaxValue ? 0 : 1;
                throw new HttpRequestException("A connection attempt failed.", new System.Net.Sockets.SocketException(10060));
            }

            return Task.CompletedTask;
        }

        public Task<bool> DatabaseExistsAsync(string database, CancellationToken cancellationToken) =>
            Task.FromResult(server.Databases.Contains(database));

        public Task<bool> CreateDatabaseAsync(string database, int replicationFactor, CancellationToken cancellationToken) =>
            Task.FromResult(server.Databases.Add(database));

        public Task<IReadOnlyList<RegisteredCertificate>> GetCertificatesAsync(string namePrefix, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RegisteredCertificate>>([.. server.Certificates
                .Where(c => c.Value.Name.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase))
                .Select(c => new RegisteredCertificate(
                    c.Value.Name,
                    c.Key,
                    c.Value.Clearance,
                    new Dictionary<string, DatabaseAccess>(c.Value.Permissions, StringComparer.OrdinalIgnoreCase)))]);

        public Task<byte[]> CreateClientCertificateAsync(string name, IReadOnlyDictionary<string, DatabaseAccess> permissions, CancellationToken cancellationToken)
        {
            var pfx = TestCertificates.CreatePfx(name);
            server.Certificates[RavenDBCloudCertificates.GetThumbprint(pfx)] = new FakeCertificate(name, new Dictionary<string, DatabaseAccess>(permissions));

            return Task.FromResult(TestCertificates.Bundle(name, pfx));
        }

        public Task DeleteCertificateAsync(string thumbprint, CancellationToken cancellationToken)
        {
            if (server.FailingDeletes > 0)
            {
                server.FailingDeletes--;
                throw new HttpRequestException("The server is unreachable.");
            }

            server.DeletedThumbprints.Add(thumbprint);
            server.Certificates.Remove(thumbprint);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}

/// <summary>Collects every log message, to assert on what the person who deploys reads.</summary>
internal sealed class CollectingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<string> Messages { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Logger(this);

    public void Dispose()
    {
    }

    private sealed class Logger(CollectingLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            provider.Messages.Enqueue(formatter(state, exception) + (exception is null ? string.Empty : " " + exception));
    }
}

internal sealed record FakeCertificate(
    string Name,
    Dictionary<string, DatabaseAccess> Permissions,
    SecurityClearance Clearance = SecurityClearance.ValidUser);

/// <summary>The process environment, as a deployment reads it.</summary>
internal sealed class FakeEnvironment : IRavenDBCloudEnvironment
{
    public Dictionary<string, string> Variables { get; } = [];

    public string? Get(string variable) => Variables.GetValueOrDefault(variable);
}

internal static class TestCertificates
{
    public static byte[] CreatePfx(string name)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

        return certificate.Export(X509ContentType.Pfx);
    }

    /// <summary>The zip RavenDB and RavenDB Cloud return: the pfx next to the PEM files.</summary>
    public static byte[] Bundle(string name, byte[] pfx)
    {
        using var zip = new MemoryStream();

        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var pem = archive.CreateEntry($"{name}.crt").Open())
            {
                pem.Write("-----BEGIN CERTIFICATE-----"u8);
            }

            using var entry = archive.CreateEntry($"{name}.pfx").Open();
            entry.Write(pfx);
        }

        return zip.ToArray();
    }
}

internal sealed class InMemoryDeploymentStateManager : IDeploymentStateManager
{
    private readonly Dictionary<string, (JsonObject Data, long Version)> _sections = new(StringComparer.Ordinal);

    public string? StateFilePath => null;

    public JsonObject? this[string sectionName] => _sections.TryGetValue(sectionName, out var section) ? section.Data : null;

    public Task<DeploymentStateSection> AcquireSectionAsync(string sectionName, CancellationToken cancellationToken = default) =>
        Task.FromResult(_sections.TryGetValue(sectionName, out var section)
            ? new DeploymentStateSection(sectionName, (JsonObject)section.Data.DeepClone(), section.Version)
            : new DeploymentStateSection(sectionName, [], 0));

    public Task SaveSectionAsync(DeploymentStateSection section, CancellationToken cancellationToken = default)
    {
        var current = _sections.TryGetValue(section.SectionName, out var existing) ? existing.Version : 0;

        if (current != section.Version)
        {
            throw new InvalidOperationException($"Concurrency conflict on '{section.SectionName}'.");
        }

        _sections[section.SectionName] = ((JsonObject)section.Data.DeepClone(), current + 1);
        section.Version = current + 1;

        return Task.CompletedTask;
    }

    public Task DeleteSectionAsync(DeploymentStateSection section, CancellationToken cancellationToken = default)
    {
        _sections.Remove(section.SectionName);
        return Task.CompletedTask;
    }

    public Task ClearAllStateAsync(CancellationToken cancellationToken = default)
    {
        _sections.Clear();
        return Task.CompletedTask;
    }
}
