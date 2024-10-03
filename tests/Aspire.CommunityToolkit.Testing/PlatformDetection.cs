namespace Aspire.Components.Common.Tests;

public static class PlatformDetection
{
    public static bool IsRunningOnCI => Environment.GetEnvironmentVariable("CI") is not null;
}