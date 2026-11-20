namespace CommunityToolkit.Aspire.Hosting.RavenDB.Cloud;

/// <summary>
/// Where a deployment reads the settings that must come from the person or the CI job running it: the process
/// environment, never configuration files, user secrets or the AppHost's code. A committed file can therefore neither
/// approve every future change nor send the API key to another endpoint. Behind an interface for the tests.
/// </summary>
internal interface IRavenDBCloudEnvironment
{
    /// <summary>The value of <paramref name="variable"/>, or <see langword="null"/> when it is not set.</summary>
    string? Get(string variable);
}

/// <summary>The environment of the process that deploys.</summary>
internal sealed class ProcessEnvironment : IRavenDBCloudEnvironment
{
    /// <inheritdoc />
    public string? Get(string variable) => Environment.GetEnvironmentVariable(variable);
}
