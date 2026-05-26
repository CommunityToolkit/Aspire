namespace CommunityToolkit.Aspire.SeaweedFS.Client;

/// <summary>
/// A strongly-typed HTTP client for interacting with the SeaweedFS Native Filer API.
/// </summary>
/// <remarks>
/// The <see cref="SeaweedFSFilerClient"/> provides a direct interface to the SeaweedFS Filer node, 
/// allowing for file system operations such as listing, creating, and managing files/directories.
/// <para>
/// <b>Usage example:</b>
/// <code>
/// public class MyService(SeaweedFSFilerClient filerClient)
/// {
///     public async Task ListFilesAsync()
///     {
///         var response = await filerClient.HttpClient.GetAsync("/");
///         // ...
///     }
/// }
/// </code>
/// </para>
/// </remarks>
/// <param name="httpClient">The configured <see cref="HttpClient"/> instance, typically managed by <see cref="IHttpClientFactory"/>.</param>
public sealed class SeaweedFSFilerClient(HttpClient httpClient)
{
    /// <summary>
    /// Gets the underlying <see cref="System.Net.Http.HttpClient"/> configured for the SeaweedFS Filer API.
    /// </summary>
    /// <value>The configured <see cref="HttpClient"/> instance.</value>
    public HttpClient HttpClient { get; } = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
}