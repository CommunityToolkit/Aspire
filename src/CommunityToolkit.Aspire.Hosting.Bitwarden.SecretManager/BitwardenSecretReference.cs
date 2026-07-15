namespace Aspire.Hosting.ApplicationModel;

internal sealed class BitwardenSecretIdExpression(BitwardenSecretResource secret) : IExpressionValue, IValueWithReferences
{
    public string ValueExpression => secret.ResolvedSecretId is Guid secretId
        ? secretId.ToString("D")
        : $"{{{secret.Parent.Name}.secrets.{secret.RemoteName}.id}}";

    IEnumerable<object> IValueWithReferences.References => [secret.Parent, secret];

    public ValueTask<string?> GetValueAsync(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(secret.ResolvedSecretId?.ToString("D"));
    }
}
