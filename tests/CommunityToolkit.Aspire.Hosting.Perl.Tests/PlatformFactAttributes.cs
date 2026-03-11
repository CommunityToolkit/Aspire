using Xunit;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

/// <summary>
/// Runs a test only on Linux and skips it on other operating systems.
/// </summary>
public sealed class LinuxOnlyFactAttribute : FactAttribute
{
    public LinuxOnlyFactAttribute()
    {
        if (!OperatingSystem.IsLinux())
        {
            Skip = "Test requires Linux.";
        }
    }
}

/// <summary>
/// Runs a test only on Windows and skips it on other operating systems.
/// </summary>
public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Test requires Windows.";
        }
    }
}
