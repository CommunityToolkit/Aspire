using Bitwarden.Sdk;
using Polly;
using Polly.Retry;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager;

internal interface IBitwardenSecretManagerProviderFactory
{
    IBitwardenSecretManagerProvider Create(string apiUrl, string identityUrl);
}

internal sealed class BitwardenSecretManagerProviderFactory : IBitwardenSecretManagerProviderFactory
{
    public IBitwardenSecretManagerProvider Create(string apiUrl, string identityUrl)
    {
        return new BitwardenSecretManagerProvider(apiUrl, identityUrl);
    }
}

internal interface IBitwardenSecretManagerProvider : IAsyncDisposable
{
    void Login(string accessToken, string? authCacheFile);

    BitwardenProjectInfo? GetProject(Guid projectId);

    BitwardenProjectInfo CreateProject(Guid organizationId, string projectName);

    BitwardenProjectInfo UpdateProject(Guid organizationId, Guid projectId, string projectName);

    BitwardenSecretInfo? GetSecret(Guid secretId);

    IReadOnlyList<BitwardenSecretInfo> GetSecretsByIds(Guid[] secretIds);

    IReadOnlyList<BitwardenSecretIdentifierInfo> ListSecrets(Guid organizationId);

    IReadOnlyList<BitwardenSecretInfo> SyncSecrets(Guid organizationId);

    BitwardenSecretInfo CreateSecret(Guid organizationId, string remoteName, string value, Guid[] projectIds, string note = "");

    BitwardenSecretInfo UpdateSecret(Guid organizationId, Guid secretId, string remoteName, string value, string note, Guid[] projectIds);
}

internal sealed class BitwardenSecretManagerProvider : IBitwardenSecretManagerProvider
{
    private readonly BitwardenClient _client;
    private readonly ResiliencePipeline _pipeline;

    public BitwardenSecretManagerProvider(string apiUrl, string identityUrl)
    {
        _client = new BitwardenClient(new BitwardenSettings
        {
            ApiUrl = apiUrl,
            IdentityUrl = identityUrl
        });

        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<BitwardenAuthException>(IsTransientError)
                    .Handle<BitwardenException>(IsTransientError),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            })
            .Build();
    }

    public void Login(string accessToken, string? authCacheFile)
    {
        _pipeline.Execute(() =>
        {
            if (string.IsNullOrWhiteSpace(authCacheFile))
            {
                _client.Auth.LoginAccessToken(accessToken);
                return;
            }

            string? directory = Path.GetDirectoryName(authCacheFile);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _client.Auth.LoginAccessToken(accessToken, authCacheFile);
        });
    }

    public BitwardenProjectInfo? GetProject(Guid projectId)
    {
        try
        {
            return _pipeline.Execute(() => Map(_client.Projects.Get(projectId)));
        }
        catch (BitwardenException ex) when (!IsTransientError(ex))
        {
            return null;
        }
    }

    public BitwardenProjectInfo CreateProject(Guid organizationId, string projectName)
        => _pipeline.Execute(() => Map(_client.Projects.Create(organizationId, projectName)));

    public BitwardenProjectInfo UpdateProject(Guid organizationId, Guid projectId, string projectName)
        => _pipeline.Execute(() => Map(_client.Projects.Update(organizationId, projectId, projectName)));

    public BitwardenSecretInfo? GetSecret(Guid secretId)
    {
        try
        {
            return _pipeline.Execute(() => Map(_client.Secrets.Get(secretId)));
        }
        catch (BitwardenException ex) when (!IsTransientError(ex))
        {
            return null;
        }
    }

    public IReadOnlyList<BitwardenSecretInfo> GetSecretsByIds(Guid[] secretIds)
    {
        if (secretIds.Length == 0)
        {
            return [];
        }

        try
        {
            SecretsResponse response = _pipeline.Execute(() => _client.Secrets.GetByIds(secretIds));
            return [.. response.Data.Select(Map)];
        }
        catch (BitwardenException ex) when (!IsTransientError(ex))
        {
            return [];
        }
    }

    public IReadOnlyList<BitwardenSecretIdentifierInfo> ListSecrets(Guid organizationId)
    {
        SecretIdentifiersResponse response = _pipeline.Execute(() => _client.Secrets.List(organizationId));
        return [.. response.Data.Select(Map)];
    }

    public IReadOnlyList<BitwardenSecretInfo> SyncSecrets(Guid organizationId)
    {
        SecretsSyncResponse secrets = _pipeline.Execute(() => _client.Secrets.Sync(organizationId, null));
        return [.. secrets.Secrets.Select(Map)];
    }

    public BitwardenSecretInfo CreateSecret(Guid organizationId, string remoteName, string value, Guid[] projectIds, string note = "")
        => _pipeline.Execute(() => Map(_client.Secrets.Create(organizationId, remoteName, value, note, projectIds)));

    public BitwardenSecretInfo UpdateSecret(Guid organizationId, Guid secretId, string remoteName, string value, string note, Guid[] projectIds)
        => _pipeline.Execute(() => Map(_client.Secrets.Update(organizationId, secretId, remoteName, value, note, projectIds)));

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    private static bool IsTransientError(Exception ex)
        => ex.Message.StartsWith("error sending request", StringComparison.OrdinalIgnoreCase);

    private static BitwardenProjectInfo Map(ProjectResponse response) => new(response.Id, response.Name, response.OrganizationId);

    private static BitwardenSecretIdentifierInfo Map(SecretIdentifierResponse response) => new(response.Id, response.Key, response.OrganizationId);

    private static BitwardenSecretInfo Map(SecretResponse response) => new(response.Id, response.Key, response.Value, response.Note, response.OrganizationId, response.ProjectId);
}

internal sealed record BitwardenProjectInfo(Guid Id, string Name, Guid OrganizationId);

internal sealed record BitwardenSecretIdentifierInfo(Guid Id, string Key, Guid OrganizationId);

internal sealed record BitwardenSecretInfo(Guid Id, string Key, string Value, string Note, Guid OrganizationId, Guid? ProjectId);
