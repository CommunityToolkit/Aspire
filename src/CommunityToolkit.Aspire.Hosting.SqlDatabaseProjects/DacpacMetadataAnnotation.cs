namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a metadata annotation that specifies the path to a .dacpac file.
/// </summary>
/// <param name="DacpacPath">Path to the .dacpac file.</param>
public record DacpacMetadataAnnotation(string DacpacPath) : IResourceAnnotation
{
}
