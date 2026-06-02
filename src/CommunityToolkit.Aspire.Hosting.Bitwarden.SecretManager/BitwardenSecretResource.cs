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
    /// <param name="localName">The caller-provided local secret name.</param>
    /// <param name="remoteName">The Bitwarden secret name.</param>
    /// <param name="parent">The owning Bitwarden resource.</param>
    /// <param name="valueGetter">Callback that resolves the secret's value from configuration.</param>
    public BitwardenSecretResource(string name, string localName, string remoteName, BitwardenSecretManagerResource parent, Func<ParameterDefault?, string> valueGetter)
        : base(name, valueGetter, secret: true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(localName);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(valueGetter);

        LocalName = localName;
        RemoteName = remoteName;
        Parent = parent;
        IsManaged = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BitwardenSecretResource"/> class for an unmanaged (reference-only) secret by remote name.
    /// </summary>
    internal BitwardenSecretResource(string name, string localName, string remoteName, BitwardenSecretManagerResource parent)
        // Empty string instead of throwing MissingParameterValueException: ParameterProcessor.ProcessParameterAsync
        // adds parameters to _unresolvedParameters when their valueGetter throws, which causes them to appear in
        // the process-parameters prompt form. Unmanaged secrets have no local value by design — their value comes
        // exclusively from Bitwarden — so they must never be prompted. Returning empty string keeps the TCS
        // resolved without entering the unresolved list; the real value flows through IValueProvider.GetValueAsync.
        : base(name, _ => string.Empty, secret: true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(localName);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        ArgumentNullException.ThrowIfNull(parent);

        LocalName = localName;
        RemoteName = remoteName;
        Parent = parent;
        IsManaged = false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BitwardenSecretResource"/> class for an unmanaged (reference-only) secret by secret identifier.
    /// </summary>
    internal BitwardenSecretResource(string name, Guid secretId, BitwardenSecretManagerResource parent)
        // See comment on the other unmanaged constructor.
        : base(name, _ => string.Empty, secret: true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(parent);

        LocalName = name;
        RemoteName = name; // placeholder; actual name resolved from Bitwarden
        Parent = parent;
        ExistingSecretId = secretId;
        IsManaged = false;
    }

    /// <summary>
    /// Gets a value indicating whether this resource is a managed secret (owned and written by Aspire)
    /// as opposed to a reference-only secret (read from an existing Bitwarden secret).
    /// </summary>
    public bool IsManaged { get; }

    internal string LocalName { get; }

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

    internal Guid? ExistingSecretId { get; set; }

    /// <summary>
    /// Gets the effective Bitwarden secret identifier: the explicitly configured ID if set, otherwise the resolved ID.
    /// </summary>
    public Guid? ResolvedSecretId => SecretId ?? ExistingSecretId;

    IEnumerable<object> IValueWithReferences.References => [Parent, this];

    string IManifestExpressionProvider.ValueExpression => SecretId is Guid secretId
        ? $"{{{Parent.Name}.secrets.{secretId:D}}}"
        : $"{{{Parent.Name}.secrets.{RemoteName}}}";

    ValueTask<string?> IValueProvider.GetValueAsync(CancellationToken cancellationToken)
    {
        // Prefer the Bitwarden-resolved value bound by the provisioner after provisioning.
        string? resolved = Parent.ResolveSecretValue(this);
        if (resolved is not null)
        {
            return ValueTask.FromResult<string?>(resolved);
        }

        // For unmanaged (reference-only) secrets, the value comes exclusively from Bitwarden.
        // Return null until the provisioner binds the value via BindResolvedSecret.
        if (!IsManaged)
        {
            return ValueTask.FromResult<string?>(null);
        }

        // For managed secrets: fall back to the ParameterResource mechanism (WaitForValueTcs set by
        // ParameterProcessor, or the configuration-backed value getter supplied at construction time).

        return GetValueAsync(cancellationToken);
    }
}
