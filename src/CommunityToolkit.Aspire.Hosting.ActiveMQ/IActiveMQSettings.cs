namespace CommunityToolkit.Aspire.Hosting.ActiveMQ;

/// <summary>
/// Represents the settings for an ActiveMQ container that differ between Artemis and Classic.
/// </summary>
public interface IActiveMQSettings
{
    /// <summary>
    /// Gets the registry for the ActiveMQ container image.
    /// </summary>
    string Registry { get; }
    /// <summary>
    /// Gets the image for the ActiveMQ container.
    /// </summary>
    string Image { get; }
    /// <summary>
    /// Gets the tag for the ActiveMQ container image.
    /// </summary>
    string Tag { get; }
    /// <summary>
    /// Gets the environment variable for the ActiveMQ server username.
    /// </summary>
    string EnvironmentVariableUsername { get; }
    /// <summary>
    /// Gets the environment variable for the ActiveMQ server password.
    /// </summary>
    string EnvironmentVariablePassword { get; }
    /// <summary>
    /// Gets the Jolokia path for the ActiveMQ container for the health-check.
    /// </summary>
    string JolokiaPath { get; }
    /// <summary>
    /// Gets the data path for the ActiveMQ container.
    /// </summary>
    string DataPath { get; }
    /// <summary>
    /// Gets the configuration path for the ActiveMQ container.
    /// </summary>
    string ConfPath { get; }
}