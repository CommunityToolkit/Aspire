namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a metadata annotation that specifies dacpac deployment options.
/// </summary>
/// <param name="OptionsPath">path to deployment options xml file</param>
public record DacDeployOptionsAnnotation(string OptionsPath) : IResourceAnnotation
{
}