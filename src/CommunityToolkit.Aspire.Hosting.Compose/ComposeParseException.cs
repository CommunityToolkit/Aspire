namespace CommunityToolkit.Aspire.Hosting.Compose;

/// <summary>
/// Exception thrown when a Docker Compose file cannot be parsed.
/// </summary>
public sealed class ComposeParseException(string message, string? filePath = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>
    /// Gets the path of the compose file that failed to parse, if available.
    /// </summary>
    public string? FilePath { get; } = filePath;
}
