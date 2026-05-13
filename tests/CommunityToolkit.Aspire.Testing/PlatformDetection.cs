namespace Aspire.Components.Common.Tests;

public static class PlatformDetection
{
    public static bool IsRunningOnGithubActions => Environment.GetEnvironmentVariable("GITHUB_JOB") is not null;
    public static bool IsRunningOnCI => IsRunningOnGithubActions || Environment.GetEnvironmentVariable("CI") is not null;

    public static bool IsWindows => OperatingSystem.IsWindows();
    public static bool IsLinux => OperatingSystem.IsLinux();
    public static bool IsMacOS => OperatingSystem.IsMacOS();
}
