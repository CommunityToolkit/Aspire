using Aspire.Hosting.JavaScript;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Node.js application running under Turborepo.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory of the application.</param>
/// <param name="filter">The Turborepo filter to use (used in 'turbo run dev --filter={filter}').</param>
/// <param name="command">The command to run (default is 'turbo').</param>
public class TurborepoAppResource(string name, string workingDirectory, string filter, string command = "turbo")
    : JavaScriptAppResource(name, command, workingDirectory)
{
    /// <summary>
    /// Gets the Turborepo filter.
    /// </summary>
    public string Filter { get; } = filter;
}