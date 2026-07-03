using Aspire.Hosting;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal interface IVercelContainerRegistryClient
{
    Task EnsureRepositoryAsync(string token, VercelOidcClaims claims, string repository, CancellationToken cancellationToken);
}

internal sealed class VercelContainerRegistryClient : IVercelContainerRegistryClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task EnsureRepositoryAsync(string token, VercelOidcClaims claims, string repository, CancellationToken cancellationToken)
    {
        // VCR repositories are project-scoped under owner/project. The integration currently
        // creates the leaf repository ("app") only; any future nested repository name should
        // be provisioned by the provider or a separate explicit API before this method runs.
        if (repository.Contains('/', StringComparison.Ordinal))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(claims.OwnerId)
            || string.IsNullOrWhiteSpace(claims.ProjectId))
        {
            throw new DistributedApplicationException("The Vercel OIDC token did not include the owner_id and project_id claims required to create the VCR repository.");
        }

        string apiUrl = (Environment.GetEnvironmentVariable("VERCEL_API_URL") ?? "https://api.vercel.com").TrimEnd('/');
        string requestUri = $"{apiUrl}/v1/vcr/repository?teamId={Uri.EscapeDataString(claims.OwnerId)}";
        string requestBody = JsonSerializer.Serialize(new
        {
            name = repository,
            projectId = claims.ProjectId
        }, JsonOptions);

        using HttpClient httpClient = new();
        using HttpRequestMessage request = new(HttpMethod.Post, requestUri)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode
            || response.StatusCode == HttpStatusCode.Conflict)
        {
            // Conflict means the repository already exists for this project, which is the
            // desired converged state for a retry or update deployment.
            return;
        }

        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new DistributedApplicationException($"Failed to create Vercel Container Registry repository '{repository}' (HTTP {(int)response.StatusCode}). {responseBody}");
    }
}
