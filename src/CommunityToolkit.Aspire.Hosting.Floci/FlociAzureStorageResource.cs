namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents the Blob, Queue and Table Storage APIs exposed by a Floci Azure emulator resource.
/// </summary>
/// <remarks>
/// The resource has no health check of its own: Aspire applies the parent emulator's health check
/// to it, so it becomes healthy as soon as the Floci Azure container is.
/// </remarks>
/// <param name="name">The name of the resource.</param>
/// <param name="parent">The parent Floci Azure emulator resource.</param>
[AspireExport(ExposeProperties = true)]
public class FlociAzureStorageResource(
    string name,
    FlociAzureContainerResource parent) : Resource(name),
    IResourceWithParent<FlociAzureContainerResource>,
    IResourceWithConnectionString
{
    internal const string DefaultName = "storage";

    /// <summary>
    /// Gets the parent Floci Azure emulator resource.
    /// </summary>
    public FlociAzureContainerResource Parent { get; } = parent ?? throw new ArgumentNullException(nameof(parent));

    /// <summary>
    /// Gets the storage account name.
    /// </summary>
    public string AccountName => FlociAzureContainerResource.DefaultAccountName;

    /// <summary>
    /// Gets the service endpoint shared by the Blob, Queue and Table APIs.
    /// </summary>
    /// <remarks>
    /// The host is the endpoint's IPv4 address rather than <c>localhost</c>: the Azure Storage SDKs
    /// only read the account name from the URL path when the host is an IP address, so a
    /// <c>localhost</c> URL makes them treat <c>localhost</c> as the account name.
    /// </remarks>
    public ReferenceExpression ServiceEndpoint =>
        ReferenceExpression.Create(
            $"{Parent.Scheme}://{Parent.PrimaryEndpoint.Property(EndpointProperty.IPV4Host)}:{Parent.Port}/{AccountName}");

    /// <summary>
    /// Gets the storage connection string expression, carrying the <c>BlobEndpoint</c>,
    /// <c>QueueEndpoint</c> and <c>TableEndpoint</c> entries so every Azure Storage SDK client
    /// resolves to the emulator.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create(
            $"DefaultEndpointsProtocol={Parent.Scheme};AccountName={AccountName};AccountKey={FlociAzureContainerResource.DefaultAccountKey};BlobEndpoint={ServiceEndpoint};QueueEndpoint={ServiceEndpoint};TableEndpoint={ServiceEndpoint};");

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties() =>
        Parent.CombineProperties([
            new("AccountName", ReferenceExpression.Create($"{AccountName}")),
            new("BlobEndpoint", ServiceEndpoint),
            new("QueueEndpoint", ServiceEndpoint),
            new("TableEndpoint", ServiceEndpoint)
        ]);
}
