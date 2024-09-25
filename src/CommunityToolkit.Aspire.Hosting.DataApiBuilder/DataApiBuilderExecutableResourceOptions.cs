namespace CommunityToolkit.Aspire.Hosting.DataApiBuilder;

/// <summary>
/// This represents the options entity for configuring an Data Api Builder.
/// </summary>
public class DataApiBuilderExecutableResourceOptions
{
    /// <summary>
    /// Gets or sets the application name. Default is <c>DAB-Api</c>.
    /// </summary>
    public string? ApplicationName { get; set; } = "DAB-Api";

    /// <summary>
    /// Gets or sets the port number. Default is <c>5000</c>.
    /// </summary>
    public int Port { get; set; } = 5000;


    /// <summary>
    /// Gets or sets the arguments to pass to the DataApiBuilder api.
    /// </summary>
    public string[]? Args { get; set; } = null;
}
