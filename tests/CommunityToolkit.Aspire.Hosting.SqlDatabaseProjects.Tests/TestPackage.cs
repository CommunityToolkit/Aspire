using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.Tests;

internal class TestPackage : IPackageMetadata
{
    public static readonly string NuGetPackageCache = Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? Environment.GetEnvironmentVariable("USERPROFILE") ?? string.Empty, ".nuget", "packages");

    public string PackageId { get; } = "ErikEJ.Dacpac.Chinook";

    public Version PackageVersion { get; } = new Version(1, 0, 0);

    public string PackagePath { get; } = Path.Combine(NuGetPackageCache, "erikej.dacpac.chinook", "1.0.0");
}