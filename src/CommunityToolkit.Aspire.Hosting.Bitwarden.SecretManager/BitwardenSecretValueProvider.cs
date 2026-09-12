using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager.Extensions;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager;

// Providers acquire values. All consumers and the common writer read ParameterResource state.
internal abstract class BitwardenSecretValueProvider
{
    internal abstract string? GetInitialValue(ParameterDefault? parameterDefault);
    internal virtual void Register(BitwardenSecretManagerResource parent, BitwardenSecretResource secret) { }
    internal virtual ReferenceExpression GetSourceExpression(BitwardenSecretResource secret) => ReferenceExpression.Create($"{new ParameterInput(secret)}");
    internal virtual IEnumerable<object> References => [];
    internal virtual Task PrepareForWriteAsync(BitwardenSecretResource secret, ValueProviderContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    private sealed class ParameterInput(BitwardenSecretResource secret) : IValueProvider, IManifestExpressionProvider
    {
        public string ValueExpression => secret.ValueExpression;
        public ValueTask<string?> GetValueAsync(CancellationToken cancellationToken) => secret.GetValueAsync(cancellationToken);
    }
}

internal sealed class BitwardenInputValueProvider(Func<ParameterDefault?, string> valueGetter) : BitwardenSecretValueProvider
{
    internal override string GetInitialValue(ParameterDefault? parameterDefault) => valueGetter(parameterDefault);
    internal override void Register(BitwardenSecretManagerResource parent, BitwardenSecretResource secret) => parent.InputSecrets.Add(secret);
}

internal sealed class BitwardenReferenceValueProvider(ReferenceExpression source, Func<string?> configuredValue) : BitwardenSecretValueProvider
{
    internal override string? GetInitialValue(ParameterDefault? parameterDefault) => configuredValue();
    internal override ReferenceExpression GetSourceExpression(BitwardenSecretResource secret) => source;
    internal override IEnumerable<object> References => [source];
    internal override void Register(BitwardenSecretManagerResource parent, BitwardenSecretResource secret) => parent.ReferenceSecrets.Add(secret);

    // Run before process-parameters. Never mistake the last generated result for an input override.
    internal void PrepareInput(BitwardenSecretResource secret) => secret.SetParameterValue(configuredValue());

    internal override async Task PrepareForWriteAsync(BitwardenSecretResource secret, ValueProviderContext context, CancellationToken cancellationToken)
    {
        secret.InitializeWaitForValue();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? value = configuredValue();
            if (string.IsNullOrEmpty(value))
            {
                value = await ((IValueProvider)source).GetValueAsync(context, cancellationToken).ConfigureAwait(false);
            }
            if (string.IsNullOrEmpty(value))
            {
                throw new DistributedApplicationException($"Managed Bitwarden secret '{secret.RemoteName}' did not resolve to a value.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            secret.SetParameterValue(value);
        }
        catch (Exception ex)
        {
            secret.SetParameterException(ex);
            throw;
        }
    }
}

internal sealed class BitwardenRemoteValueProvider : BitwardenSecretValueProvider
{
    internal override string? GetInitialValue(ParameterDefault? parameterDefault) => null;
}