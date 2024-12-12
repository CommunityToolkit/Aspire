using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// 
/// </summary>
public interface IPackageMetadata : IResourceAnnotation
{
    /// <summary>
    /// 
    /// </summary>
    string PackageId { get; }
    /// <summary>
    /// 
    /// </summary>
    string PackagePath { get; }
}