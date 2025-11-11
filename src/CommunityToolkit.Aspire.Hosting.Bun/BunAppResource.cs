namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Bun app resource.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory for the Bun app to launch from.</param>
public class BunAppResource(string name, string workingDirectory) :
    ExecutableResource(name, "bun", workingDirectory);