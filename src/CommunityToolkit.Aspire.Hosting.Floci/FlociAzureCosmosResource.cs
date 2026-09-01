namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents the Cosmos DB API exposed by a Floci Azure emulator resource.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="accountName">The Cosmos DB account name.</param>
/// <param name="parent">The parent Floci Azure emulator resource.</param>
[AspireExport(ExposeProperties = true)]
public class FlociAzureCosmosResource(
    string name,
    string accountName,
    FlociAzureContainerResource parent) : Resource(name),
    IResourceWithParent<FlociAzureContainerResource>,
    IResourceWithConnectionString
{
    internal const string DefaultName = "cosmos";

    // Well-known Cosmos DB emulator account key that floci-az accepts by default (no auth enforced).
    internal const string DefaultAccountKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    /// <summary>
    /// Gets the parent Floci Azure emulator resource.
    /// </summary>
    public FlociAzureContainerResource Parent { get; } = parent ?? throw new ArgumentNullException(nameof(parent));

    /// <summary>
    /// Gets the Cosmos DB account name.
    /// </summary>
    public string AccountName { get; } = string.IsNullOrWhiteSpace(accountName)
        ? throw new ArgumentException("The account name cannot be empty or whitespace.", nameof(accountName))
        : accountName;

    /// <summary>
    /// Gets the Cosmos DB account endpoint.
    /// </summary>
    public ReferenceExpression AccountEndpoint =>
        ReferenceExpression.Create($"{Parent.ConnectionStringExpression}/{AccountName}-cosmos/");

    /// <summary>
    /// Gets the Cosmos DB connection string expression.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"AccountEndpoint={AccountEndpoint};AccountKey={DefaultAccountKey};");

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties() =>
        Parent.CombineProperties([
            new("AccountEndpoint", AccountEndpoint),
            new("AccountName", ReferenceExpression.Create($"{AccountName}"))
        ]);
}
