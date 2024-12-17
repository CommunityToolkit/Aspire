using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.Tests;

internal class TestPackage : IPackageMetadata
{
    public static readonly string NuGetPackageCache = Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? Environment.GetEnvironmentVariable("USERPROFILE") ?? string.Empty, ".nuget", "packages");

    public string PackageId { get; } = "Microsoft.SqlServer.Dacpacs.Master";

    public Version PackageVersion { get; } = new Version(160, 2, 3);

    public string PackagePath { get; } = Path.Combine(NuGetPackageCache, "microsoft.sqlserver.dacpacs.master", "160.2.3");
}