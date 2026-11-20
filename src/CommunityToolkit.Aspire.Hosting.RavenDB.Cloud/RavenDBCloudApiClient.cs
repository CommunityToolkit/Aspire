using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Hosting.RavenDB.Cloud;

/// <summary>Creates <see cref="RavenDBCloudApiClient"/> instances; replaced in tests.</summary>
internal interface IRavenDBCloudApiClientFactory
{
    RavenDBCloudApiClient Create(string endpoint, string apiKey);
}

/// <summary>Creates the clients of the real Cloud API: no redirects, and a two-minute timeout per request.</summary>
internal sealed class RavenDBCloudApiClientFactory : IRavenDBCloudApiClientFactory
{
    // No redirects: HttpClient would send the X-Api-Key header, which has account-owner rights, to wherever they point.
    /// <inheritdoc />
    public RavenDBCloudApiClient Create(string endpoint, string apiKey) =>
        new(new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(2) }, endpoint, apiKey, ownsHttpClient: true);
}

/// <summary>
/// Client for the RavenDB Cloud API v1 (<c>https://api.cloud.ravendb.net/api/v1/swagger.json</c>). Authentication is
/// an account-level API key sent in the <c>X-Api-Key</c> header.
/// </summary>
internal sealed class RavenDBCloudApiClient : IDisposable
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;

    public RavenDBCloudApiClient(HttpClient http, string endpoint, string apiKey, bool ownsHttpClient = false)
    {
        _http = http;
        _ownsHttpClient = ownsHttpClient;

        _http.BaseAddress = new Uri(endpoint.TrimEnd('/') + "/");
        _http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // The Cloud API's gateway answers requests without a User-Agent, which HttpClient does not send, with 403.
        _http.DefaultRequestHeaders.UserAgent.Add(UserAgent);
    }

    /// <summary>Delay before the first retry, doubled for each further one; shortened in tests.</summary>
    internal TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    private const int MaxAttempts = 5;

    /// <summary>The User-Agent of every request, with the package version; the API's gateway refuses requests without one.</summary>
    internal static ProductInfoHeaderValue UserAgent { get; } = new(
        "CommunityToolkit.Aspire.Hosting.RavenDB.Cloud",
        typeof(RavenDBCloudApiClient).Assembly.GetName().Version?.ToString(3) ?? "1.0.0");

    /// <summary>GET /api/v1/products/list.</summary>
    public async Task<IReadOnlyList<ProductListItem>> ListProductsAsync(CancellationToken cancellationToken)
    {
        using var response = await GetAsync("api/v1/products/list", cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "list products", cancellationToken).ConfigureAwait(false);

        var list = await response.Content.ReadFromJsonAsync<ProductListResponse>(s_json, cancellationToken).ConfigureAwait(false);

        return list?.Items ?? [];
    }

    /// <summary>GET /api/v1/products/details/{id}; <see langword="null"/> when the product is gone.</summary>
    public async Task<ProductDetails?> GetProductAsync(string productId, CancellationToken cancellationToken)
    {
        using var response = await GetAsync($"api/v1/products/details/{Uri.EscapeDataString(productId)}", cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return null;
        }

        await EnsureSuccessAsync(response, "get product details", cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync<ProductDetails>(s_json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>POST /api/v1/products/create; returns the new product id.</summary>
    public async Task<string> CreateProductAsync(ProductCreateRequest request, CancellationToken cancellationToken)
    {
        // Not retried on a server error: the product may have been created, and the next deployment finds it by name.
        using var response = await SendAsync(() => Post("api/v1/products/create", request), idempotent: false, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "create the product", cancellationToken).ConfigureAwait(false);

        var created = await response.Content.ReadFromJsonAsync<ProductCreatedResponse>(s_json, cancellationToken).ConfigureAwait(false);

        return created?.ProductId ?? throw new InvalidOperationException("RavenDB Cloud did not return a product id.");
    }

    /// <summary>POST /api/v1/products/instance-type.</summary>
    public async Task ChangeInstanceTypeAsync(string productId, string instanceType, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () => Post("api/v1/products/instance-type", new ChangeInstanceTypeRequest(productId, instanceType)),
            idempotent: false,
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "change the instance type", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>POST /api/v1/products/storage.</summary>
    public async Task ChangeStorageAsync(ChangeStorageRequest request, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(() => Post("api/v1/products/storage", request), idempotent: false, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "change the storage", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>GET /api/v1/products/security/certificate/{id}: the bundle with the client admin certificate.</summary>
    public async Task<byte[]> GetAdminCertificateAsync(string productId, CancellationToken cancellationToken)
    {
        using var response = await GetAsync($"api/v1/products/security/certificate/{Uri.EscapeDataString(productId)}", cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "download the client admin certificate", cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// POST /api/v1/products/terminate/{id}, hiding the product in the portal: RavenDB Cloud keeps a product's name taken
    /// until it is hidden, and the next deployment may want the name back. A product that is already gone is not an error.
    /// </summary>
    public async Task TerminateProductAsync(string productId, CancellationToken cancellationToken)
    {
        // Terminating twice changes nothing: a product already gone answers 404.
        using var response = await SendAsync(
            () => Post($"api/v1/products/terminate/{Uri.EscapeDataString(productId)}", new TerminateProductRequest(HideInPortal: true)),
            idempotent: true,
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return;
        }

        await EnsureSuccessAsync(response, "terminate the product", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>GET /api/v1/metadata/regions/{provider}.</summary>
    public async Task<IReadOnlyList<string>> GetRegionsAsync(string provider, CancellationToken cancellationToken)
    {
        using var response = await GetAsync($"api/v1/metadata/regions/{Uri.EscapeDataString(provider)}", cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "list regions", cancellationToken).ConfigureAwait(false);

        var regions = await response.Content.ReadFromJsonAsync<RegionsResponse>(s_json, cancellationToken).ConfigureAwait(false);

        return regions?.Names ?? [];
    }

    /// <summary>GET /api/v1/metadata/instance-types/{provider}/{region}.</summary>
    public async Task<IReadOnlyList<InstanceTypeItem>> GetInstanceTypesAsync(string provider, string region, CancellationToken cancellationToken)
    {
        using var response = await GetAsync(
            $"api/v1/metadata/instance-types/{Uri.EscapeDataString(provider)}/{Uri.EscapeDataString(region)}",
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "list instance types", cancellationToken).ConfigureAwait(false);

        var types = await response.Content.ReadFromJsonAsync<InstanceTypesResponse>(s_json, cancellationToken).ConfigureAwait(false);

        return types?.InstanceTypes ?? [];
    }

    /// <summary>GET /api/v1/metadata/release-channels.</summary>
    public async Task<ReleaseChannelsResponse> GetReleaseChannelsAsync(CancellationToken cancellationToken)
    {
        using var response = await GetAsync("api/v1/metadata/release-channels", cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "list release channels", cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync<ReleaseChannelsResponse>(s_json, cancellationToken).ConfigureAwait(false)
            ?? new ReleaseChannelsResponse(null, null);
    }

    private Task<HttpResponseMessage> GetAsync(string path, CancellationToken cancellationToken) =>
        SendAsync(() => new HttpRequestMessage(HttpMethod.Get, path), idempotent: true, cancellationToken);

    private static HttpRequestMessage Post<T>(string path, T body) =>
        new(HttpMethod.Post, path) { Content = JsonContent.Create(body, options: s_json) };

    /// <summary>
    /// The API accepts about one request per second and answers 429 beyond that. A 429 was not processed, so any
    /// request is sent again; a server error or a lost connection only when sending it again changes nothing.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> request, bool idempotent, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            HttpResponseMessage response;

            try
            {
                using var message = request();
                response = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException) when (idempotent && attempt < MaxAttempts)
            {
                await Task.Delay(RetryDelay * Math.Pow(2, attempt - 1), cancellationToken).ConfigureAwait(false);
                continue;
            }

            var retry = response.StatusCode == HttpStatusCode.TooManyRequests || (idempotent && (int)response.StatusCode >= 500);

            if (!retry || attempt == MaxAttempts)
            {
                return response;
            }

            var delay = response.Headers.RetryAfter?.Delta ?? RetryDelay * Math.Pow(2, attempt - 1);
            response.Dispose();
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var hint = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => " Check the RavenDB Cloud API key.",

            // The API's answer when the account already holds its quota of paid products.
            HttpStatusCode.PreconditionFailed =>
                " The account has reached its quota of products: terminate one in the RavenDB Cloud portal, or ask support to raise the quota.",
            _ => string.Empty,
        };

        throw new InvalidOperationException(
            $"RavenDB Cloud API failed to {operation}: {(int)response.StatusCode} {response.ReasonPhrase}.{hint} {body}".TrimEnd());
    }

    /// <summary>Disposes the HTTP client when this client created it.</summary>
    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
    }
}

/// <summary>The answer of <c>GET /api/v1/products/list</c>.</summary>
internal sealed record ProductListResponse(IReadOnlyList<ProductListItem>? Items);

/// <summary>A product in <see cref="ProductListResponse"/>.</summary>
internal sealed record ProductListItem(string? Id, string? Name);

/// <summary>The body of <c>POST /api/v1/products/create</c>.</summary>
internal sealed record ProductCreateRequest(
    string CloudProvider,
    string InstanceTypeName,
    string DisplayName,
    string? ReleaseChannel,
    string? SubdomainName,
    string Tier,
    string Region,
    int DiskSize,
    int? Iops,
    double? Throughput,
    string StorageTypeName,
    IReadOnlyList<string> AllowedIps,
    string DiskLayout);

/// <summary>The answer of <c>POST /api/v1/products/create</c>.</summary>
internal sealed record ProductCreatedResponse(string? ProductId);

/// <summary>The body of the request that changes a product's instance type.</summary>
internal sealed record ChangeInstanceTypeRequest(string ProductId, string InstanceType);

/// <summary>The body of the request that changes a product's data disk.</summary>
internal sealed record ChangeStorageRequest(string ProductId, string StorageType, int Size, int? Iops, double? Throughput);

/// <summary>The answer of <c>GET /api/v1/products/details/{id}</c>, the parts the deployment reads.</summary>
internal sealed record ProductDetails(
    string? Id,
    string? DisplayName,
    string? Status,
    string? Tier,
    string? CloudProvider,
    string? Region,
    string? InstanceType,
    string? ReleaseChannel,
    IReadOnlyList<string>? Dns,
    IReadOnlyList<string>? NodeTags,
    HardwareInfo? HardwareInfo,
    ProductSecurity? Security);

/// <summary>
/// A product's disks. One with a separate data disk, set up in the portal, reports its system disk as
/// <paramref name="Storage"/> and the data disk as <paramref name="AdditionalStorage"/>.
/// </summary>
internal sealed record HardwareInfo(StorageInfo? Storage, StorageInfo? AdditionalStorage)
{
    /// <summary>The disk the data is on, which the spec describes.</summary>
    public StorageInfo? DataDisk => AdditionalStorage ?? Storage;
}

/// <summary>A disk of a product: size in GB, IOPS, throughput in MB/s and storage type.</summary>
internal sealed record StorageInfo(int Size, int? Iops, double? Throughput, string? Type);

/// <summary>The network settings of a product.</summary>
internal sealed record ProductSecurity(IReadOnlyList<string>? AllowedIps);

/// <summary>The body of the request that terminates a product.</summary>
internal sealed record TerminateProductRequest(bool HideInPortal);

/// <summary>The regions a provider offers the account.</summary>
internal sealed record RegionsResponse(IReadOnlyList<string>? Names);

/// <summary>The instance types a region offers the account.</summary>
internal sealed record InstanceTypesResponse(IReadOnlyList<InstanceTypeItem>? InstanceTypes);

/// <summary>An instance type in <see cref="InstanceTypesResponse"/>.</summary>
internal sealed record InstanceTypeItem(string? Name, string? Tier, InstanceTypeParameters? Parameters);

/// <summary>What an instance type is: vCPUs, RAM in GB, nodes, and the disk sizes and storage types it offers.</summary>
internal sealed record InstanceTypeParameters(
    int? VirtualCpus,
    double? Ram,
    int? NumberOfNodes,
    IReadOnlyList<int>? AvailableDiskSizes,
    IReadOnlyList<StorageTypeInfo>? AvailableStorageTypes);

/// <summary>A storage type an instance type offers.</summary>
internal sealed record StorageTypeInfo(string? Name, IopsRange? IopsRange);

/// <summary>The IOPS a storage type can be provisioned with.</summary>
internal sealed record IopsRange(int Min, int Max);

/// <summary>The release channels the account can use.</summary>
internal sealed record ReleaseChannelsResponse(string? DefaultReleaseChannel, IReadOnlyList<ReleaseChannelItem>? ReleaseChannels);

/// <summary>A release channel and the RavenDB version it is on.</summary>
internal sealed record ReleaseChannelItem(string? Name, string? RavenVersion);

/// <summary>Values of <c>ProductStatus</c>.</summary>
internal static class ProductStatus
{
    /// <summary>Being created; it has no URL yet.</summary>
    public const string Creating = "Creating";
    /// <summary>Running; changes can be requested.</summary>
    public const string Active = "Active";
    /// <summary>Applying a change.</summary>
    public const string DeployingChanges = "DeployingChanges";
    /// <summary>Being terminated.</summary>
    public const string Terminating = "Terminating";
    /// <summary>Terminated; its name stays taken until it is hidden in the portal.</summary>
    public const string Terminated = "Terminated";
    /// <summary>Failed in RavenDB Cloud.</summary>
    public const string Error = "Error";
    /// <summary>Waiting for a payment before RavenDB Cloud creates it.</summary>
    public const string AwaitingPayment = "AwaitingPayment";
}
