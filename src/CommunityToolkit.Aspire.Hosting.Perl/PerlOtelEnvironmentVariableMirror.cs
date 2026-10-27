using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Perl;

/// <summary>
/// Resolves a Perl-specific OpenTelemetry variable from the final value of its standard variable.
/// </summary>
/// <param name="environmentVariables">The environment variables shared by the resource's environment callbacks.</param>
/// <param name="sourceName">The standard OpenTelemetry variable to read.</param>
internal sealed class PerlOtelEnvironmentVariableMirror(
    IReadOnlyDictionary<string, object> environmentVariables,
    string sourceName) : IValueProvider, IValueWithReferences
{
    /// <inheritdoc />
    public IEnumerable<object> References =>
        environmentVariables.TryGetValue(sourceName, out var value) && !ReferenceEquals(value, this)
            ? [value]
            : [];

    /// <inheritdoc />
    public ValueTask<string?> GetValueAsync(CancellationToken cancellationToken = default) =>
        GetValueAsync(new ValueProviderContext(), cancellationToken);

    /// <inheritdoc />
    public async ValueTask<string?> GetValueAsync(ValueProviderContext context, CancellationToken cancellationToken = default)
    {
        if (!environmentVariables.TryGetValue(sourceName, out var value) || ReferenceEquals(value, this))
        {
            return null;
        }

        return value switch
        {
            string s => s,
            IValueProvider provider => await provider.GetValueAsync(context, cancellationToken).ConfigureAwait(false),
            IResourceBuilder<IResource> { Resource: IValueProvider provider } =>
                await provider.GetValueAsync(context, cancellationToken).ConfigureAwait(false),
            _ => value.ToString()
        };
    }

    /// <inheritdoc />
    public override string ToString() => sourceName;
}
