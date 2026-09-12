#pragma warning disable ASPIREATS001

using CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager;

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
        : this(name, remoteName, parent, new BitwardenInputValueProvider(valueGetter), isManaged: true)
    {
        ArgumentNullException.ThrowIfNull(valueGetter);
    }

    private BitwardenSecretResource(string name, string remoteName, BitwardenSecretManagerResource parent, BitwardenSecretValueProvider valueProvider, bool isManaged)
        : base(name, parameterDefault => valueProvider.GetInitialValue(parameterDefault)!, secret: true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        ArgumentNullException.ThrowIfNull(parent);
        RemoteName = remoteName;
        Parent = parent;
        IsManaged = isManaged;
        ValueProvider = valueProvider;
    }
    /// <summary>
    /// Initializes a new instance of the <see cref="BitwardenSecretResource"/> class for an unmanaged (reference-only) secret by remote name.
    /// </summary>
    internal BitwardenSecretResource(string name, string remoteName, BitwardenSecretManagerResource parent)
        : this(name, remoteName, parent, new BitwardenRemoteValueProvider(), isManaged: false)
    {
    }
    /// <summary>
    /// Initializes a new instance of the <see cref="BitwardenSecretResource"/> class for an unmanaged (reference-only) secret by secret identifier.
    /// </summary>
    internal BitwardenSecretResource(string name, Guid secretId, BitwardenSecretManagerResource parent)
        : this(name, name, parent)
    {
        ExistingSecretId = secretId;
    }

    internal BitwardenSecretResource(string name, string remoteName, BitwardenSecretManagerResource parent, ReferenceExpression value, Func<string?>? configuredValue = null)
        : this(name, remoteName, parent, new BitwardenReferenceValueProvider(value, configuredValue ?? (() => null)), isManaged: true)
    {
        ArgumentNullException.ThrowIfNull(value);
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

    internal BitwardenSecretValueProvider ValueProvider { get; }

    internal Guid? ExistingSecretId { get; }

    /// <summary>
    /// Gets the effective Bitwarden secret identifier: the explicitly configured ID if set, otherwise the resolved ID.
    /// </summary>
    public Guid? ResolvedSecretId => SecretId ?? ExistingSecretId;

    IEnumerable<object> IValueWithReferences.References => [Parent, this, .. ValueProvider.References];

    string IManifestExpressionProvider.ValueExpression => SecretId is Guid secretId
        ? $"{{{Parent.Name}.secrets.{secretId:D}}}"
        : $"{{{Parent.Name}.secrets.{RemoteName}}}";

}