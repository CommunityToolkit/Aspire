namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a resource for Sqlite Web with a specified name.
/// </summary>
/// <param name="name">The name of the resource.</param>
public class SqliteWebResource(string name) : ContainerResource(name)
{
}