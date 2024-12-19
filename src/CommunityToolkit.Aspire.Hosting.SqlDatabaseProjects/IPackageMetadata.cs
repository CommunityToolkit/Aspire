using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Represents metadata for a referenced NuGet package.
/// </summary>
public interface IPackageMetadata : IResourceAnnotation
{
    /// <summary>
    /// Gets the unique identifier of the package.
    /// </summary>
    string PackageId { get; }

    /// <summary>
    /// Gets the version of the package.
    /// </summary>
    Version PackageVersion { get; }

    /// <summary>
    /// Gets the physical location on disk of the package.
    /// </summary>
    string PackagePath { get; }
}