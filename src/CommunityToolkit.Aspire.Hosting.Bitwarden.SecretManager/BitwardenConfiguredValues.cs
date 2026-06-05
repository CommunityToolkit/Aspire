namespace Aspire.Hosting.ApplicationModel;

internal sealed class BitwardenProjectIdReference(BitwardenSecretManagerResource resource) : IManifestExpressionProvider, IValueProvider, IValueWithReferences
{
    public string ValueExpression => $"{{{resource.Name}.projectId}}";

    IEnumerable<object> IValueWithReferences.References => [resource];

    public ValueTask<string?> GetValueAsync(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(resource.ProjectId?.ToString("D"));
    }
}
