namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a managed Bitwarden secret resource.
/// </summary>
public class BitwardenSecretResource : Resource, IResourceWithParent<BitwardenSecretManagerResource>, IBitwardenSecretReference
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BitwardenSecretResource"/> class.
    /// </summary>
    /// <param name="name">The internal Aspire resource name.</param>
    /// <param name="localName">The caller-provided local secret name.</param>
    /// <param name="remoteName">The Bitwarden secret name.</param>
    /// <param name="parent">The owning Bitwarden resource.</param>
    /// <param name="value">The secret value source.</param>
    public BitwardenSecretResource(string name, string localName, string remoteName, BitwardenSecretManagerResource parent, object value)
        : base(name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(localName);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(value);

        LocalName = localName;
        RemoteName = remoteName;
        Parent = parent;
        Value = value;
    }

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

    /// <summary>
    /// Gets the value source used to manage the Bitwarden secret.
    /// </summary>
    public object Value { get; }

    internal Guid? ExistingSecretId { get; set; }

    BitwardenSecretManagerResource IBitwardenSecretReference.Resource => Parent;

    Guid? IBitwardenSecretReference.SecretId => SecretId ?? ExistingSecretId;

    string? IBitwardenSecretReference.RemoteName => RemoteName;

    IResource? IBitwardenSecretReference.SecretOwner
    {
        get => this;
        set { }
    }

    string IManifestExpressionProvider.ValueExpression => SecretId is Guid secretId
        ? $"{{{Parent.Name}.secrets.{secretId:D}}}"
        : $"{{{Parent.Name}.secrets.{RemoteName}}}";

    ValueTask<string?> IValueProvider.GetValueAsync(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(Parent.ResolveSecretValue(this));
    }
}