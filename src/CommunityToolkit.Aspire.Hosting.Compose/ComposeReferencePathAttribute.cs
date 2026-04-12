namespace CommunityToolkit.Aspire.Hosting.Compose;

/// <summary>
/// Indicates the path to the Docker Compose file associated with a source-generated class.
/// Applied by the compose source generator to generated <c>Compose.*</c> classes.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ComposeReferencePathAttribute : Attribute
{
    /// <summary>
    /// Gets the path to the compose file.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ComposeReferencePathAttribute"/> class.
    /// </summary>
    /// <param name="path">The path to the compose file.</param>
    public ComposeReferencePathAttribute(string path) => Path = path;
}
