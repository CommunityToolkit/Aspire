using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.RavenDB.Cloud;

/// <summary>
/// A value the RavenDB Cloud deployment only knows once it has found or created the product. The published artifacts
/// carry a placeholder for it (in Docker Compose, a variable of the .env file); <c>aspire deploy</c> sets it before the
/// compute environments resolve their values.
/// </summary>
/// <param name="expression">The manifest expression, such as <c>{ravendb.url}</c>; Docker Compose names the variable after it.</param>
internal abstract class RavenDBCloudDeployTimeValue(string expression) : IValueProvider, IManifestExpressionProvider
{
    /// <summary>The value; <see langword="null"/> until the deployment sets it.</summary>
    public string? Value { get; set; }

    /// <inheritdoc />
    public string ValueExpression { get; } = expression;

    /// <inheritdoc />
    public ValueTask<string?> GetValueAsync(CancellationToken cancellationToken = default) => new(Value);

    /// <summary>The manifest expression.</summary>
    public override string ToString() => ValueExpression;
}

/// <summary>The URL of the product.</summary>
internal sealed class RavenDBCloudEndpoint(RavenDBServerResource server)
    : RavenDBCloudDeployTimeValue($"{{{server.Name}.url}}");

/// <summary>An application's client certificate, as base64-encoded .pfx content.</summary>
internal sealed class RavenDBCloudCertificate(RavenDBServerResource server, IResource consumer)
    : RavenDBCloudDeployTimeValue($"{{{server.Name}-{consumer.Name}.certificate}}")
{
    /// <summary>The application the certificate is for.</summary>
    public IResource Consumer { get; } = consumer;

    /// <summary>The thumbprint of the certificate in <see cref="RavenDBCloudDeployTimeValue.Value"/>.</summary>
    public string? Thumbprint { get; set; }
}
