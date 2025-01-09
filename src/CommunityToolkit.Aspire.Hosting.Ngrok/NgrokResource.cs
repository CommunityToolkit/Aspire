namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a ngrok resource.
/// </summary>
/// <param name="name">The name of the resource.</param>
public class NgrokResource(string name) : ContainerResource(name);