namespace Aspire.Components.Common.Tests;

public static class PlatformDetection
{
    public static bool IsRunningOnAzdoBuildMachine => Environment.GetEnvironmentVariable("BUILD_BUILDID") is not null;
    public static bool IsRunningOnHelix => Environment.GetEnvironmentVariable("HELIX_WORKITEM_ROOT") is not null;
    public static bool IsRunningOnGithubActions => Environment.GetEnvironmentVariable("GITHUB_JOB") is not null;
    public static bool IsRunningOnCI => IsRunningOnAzdoBuildMachine || IsRunningOnHelix || IsRunningOnGithubActions;
    public static bool IsRunningFromAzdo => IsRunningOnAzdoBuildMachine || IsRunningOnHelix;
    public static bool IsRunningPRValidation => IsRunningOnGithubActions;

    public static bool IsWindows => OperatingSystem.IsWindows();
    public static bool IsLinux => OperatingSystem.IsLinux();
    public static bool IsMacOS => OperatingSystem.IsMacOS();
}
