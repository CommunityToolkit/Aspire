#pragma warning disable ASPIREATS001

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Bitwarden secret resource.
/// </summary>
[AspireExport]
public class BitwardenSecretResource : ParameterResource, IResourceWithParent<BitwardenSecretManagerResource>, IManifestExpressionProvider, IValueProvider, IValueWithReferences
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BitwardenSecretResource"/> class for a managed secret.
    /// </summary>
    /// <param name="name">The internal Aspire resource name.</param>
    /// <param name="remoteName">The Bitwarden secret name.</param>
    /// <param name="parent">The owning Bitwarden resource.</param>
    /// <param name="valueGetter">Callback that resolves the secret's value from configuration.</param>
    public BitwardenSecretResource(string name, string remoteName, BitwardenSecretManagerResource parent, Func<ParameterDefault?, string> valueGetter)
        : base(name, valueGetter, secret: true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(valueGetter);

        RemoteName = remoteName;
        Parent = parent;
        IsManaged = true;
        AcceptsParameterInput = true;
        ValueSource = ReferenceExpression.Create($"{new ParameterValueReference(this)}");
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BitwardenSecretResource"/> class for an unmanaged (reference-only) secret by remote name.
    /// </summary>
    internal BitwardenSecretResource(string name, string remoteName, BitwardenSecretManagerResource parent)
        // Reference-only secrets have no parameter input. Returning empty string keeps them
        // out of ParameterProcessor's prompt list; their value provider reads Bitwarden instead.
        : base(name, _ => string.Empty, secret: true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        ArgumentNullException.ThrowIfNull(parent);

        RemoteName = remoteName;
        Parent = parent;
        IsManaged = false;
        ValueSource = new RemoteValueReference(this);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BitwardenSecretResource"/> class for an unmanaged (reference-only) secret by secret identifier.
    /// </summary>
    internal BitwardenSecretResource(string name, Guid secretId, BitwardenSecretManagerResource parent)
        : this(name, name, parent)
    {
        ExistingSecretId = secretId;
    }

    // Explicit outputs are not parameter inputs. The base getter never prompts or reads saved
    // parameter state; the supplied expression resolves the output when its owner is ready.
    internal BitwardenSecretResource(string name, string remoteName, BitwardenSecretManagerResource parent, ReferenceExpression value)
        : this(name, remoteName, parent, _ => string.Empty)
    {
        ArgumentNullException.ThrowIfNull(value);
        AcceptsParameterInput = false;
        ValueSource = value;
    }

    /// <summary>
    /// Gets a value indicating whether this resource is a managed secret (owned and written by Aspire)
    /// as opposed to a reference-only secret (read from an existing Bitwarden secret).
    /// </summary>
    public bool IsManaged { get; }

    /// <summary>
    /// Gets the Bitwarden secret name.
    /// </summary>
    public string RemoteName { get; }

    /// <summary>
    /// Gets the resolved Bitwarden secret identifier after initialization.
    /// </summary>
    public Guid? SecretId { get; internal set; }

    /// <summary>
    /// Gets the owning Bitwarden resource.
    /// </summary>
    public BitwardenSecretManagerResource Parent { get; }

    // Input reconciliation is separate from value evaluation. Only parameter-backed managed
    // secrets may obtain their input from configuration, prompts, or Bitwarden pre-sync.
    internal bool AcceptsParameterInput { get; }

    internal IValueProvider ValueSource { get; }

    internal Guid? ExistingSecretId { get; }

    /// <summary>
    /// Gets the effective Bitwarden secret identifier: the explicitly configured ID if set, otherwise the resolved ID.
    /// </summary>
    public Guid? ResolvedSecretId => SecretId ?? ExistingSecretId;

    IEnumerable<object> IValueWithReferences.References => [Parent, this, ValueSource];

    string IManifestExpressionProvider.ValueExpression => SecretId is Guid secretId
        ? $"{{{Parent.Name}.secrets.{secretId:D}}}"
        : $"{{{Parent.Name}.secrets.{RemoteName}}}";

    ValueTask<string?> IValueProvider.GetValueAsync(CancellationToken cancellationToken) =>
        ValueSource.GetValueAsync(cancellationToken);

    // Both interface overloads evaluate the same source. ParameterResource's public methods
    // remain the parameter-input path used by Aspire's parameter processor.
    ValueTask<string?> IValueProvider.GetValueAsync(ValueProviderContext context, CancellationToken cancellationToken) =>
        ValueSource.GetValueAsync(context, cancellationToken);

    private sealed class ParameterValueReference(BitwardenSecretResource resource) : IValueProvider, IManifestExpressionProvider
    {
        public string ValueExpression => resource.ValueExpression;

        public ValueTask<string?> GetValueAsync(CancellationToken cancellationToken) =>
            resource.Parent.ResolveSecretValue(resource) is { } resolved
                ? ValueTask.FromResult<string?>(resolved)
                // Call the inherited parameter method directly. Referring to resource through
                // IValueProvider here would evaluate ValueSource again and recurse.
                : resource.GetValueAsync(cancellationToken);
    }

    private sealed class RemoteValueReference(BitwardenSecretResource resource) : IValueProvider
    {
        // A plain provider preserves null until resolution. Interpolating a missing remote
        // value into a ReferenceExpression would turn it into an empty, resolved string.
        public ValueTask<string?> GetValueAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(resource.Parent.ResolveSecretValue(resource));
    }
}