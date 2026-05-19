#pragma warning disable ASPIREATS001

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a reference to a Bitwarden Secrets Manager secret.
/// </summary>
[AspireExport]
public interface IBitwardenSecretReference : IExpressionValue, IValueProvider, IManifestExpressionProvider, IValueWithReferences
{
    /// <summary>
    /// Gets the Bitwarden resource that owns the secret reference.
    /// </summary>
    BitwardenSecretManagerResource Resource { get; }

    /// <summary>
    /// Gets the remote secret name, if the reference was declared by name.
    /// </summary>
    string? RemoteName { get; }

    /// <summary>
    /// Gets the remote secret identifier, if the reference was declared by identifier.
    /// </summary>
    Guid? SecretId { get; }

    /// <summary>
    /// Gets the secret owner resource, when the reference is backed by a managed secret resource.
    /// </summary>
    IResource? SecretOwner { get => throw new NotImplementedException(); }

    IEnumerable<object> IValueWithReferences.References => SecretOwner is null ? [Resource] : [Resource, SecretOwner];
}

internal sealed class BitwardenSecretReference(string? remoteName, Guid? secretId, BitwardenSecretManagerResource resource) : IBitwardenSecretReference
{
    public BitwardenSecretManagerResource Resource => resource;

    public string? RemoteName => remoteName;

    public Guid? SecretId => secretId;

    public IResource? SecretOwner => remoteName is null ? null : resource.FindManagedSecretByRemoteName(remoteName);

    public string ValueExpression => secretId is Guid id
        ? $"{{{resource.Name}.secrets.{id:D}}}"
        : $"{{{resource.Name}.secrets.{remoteName}}}";

    public ValueTask<string?> GetValueAsync(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(resource.ResolveSecretValue(this));
    }
}