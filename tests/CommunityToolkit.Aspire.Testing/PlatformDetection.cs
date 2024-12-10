namespace Aspire.Components.Common.Tests;

public static class PlatformDetection
{
    public static bool IsRunningOnCI => Environment.GetEnvironmentVariable("CI") is not null;

    public static bool IsRunningOnGitHubActions =>  Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is not null;
}