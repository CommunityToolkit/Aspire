using Aspire.Hosting.ApplicationModel;
namespace Aspire.Hosting;

/// <summary>
/// Represents an annotation for defining a config set for a Solr resource.
/// </summary>
public sealed class SolrConfigSetAnnotation : IResourceAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SolrConfigSetAnnotation"/> class.
    /// </summary>
    /// <param name="configSetName">The name of the config set.</param>
    /// <param name="configSetPath">The path to the config set directory.</param>
    public SolrConfigSetAnnotation(string configSetName, string configSetPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(configSetName);
        ArgumentException.ThrowIfNullOrEmpty(configSetPath);
        ConfigSetName = configSetName;
        ConfigSetPath = configSetPath;
    }

    /// <summary>
    /// Gets the name of the config set.
    /// </summary>
    public string ConfigSetName { get; }

    /// <summary>
    /// Gets the path to the config set directory.
    /// </summary>
    public string ConfigSetPath { get; }
}
