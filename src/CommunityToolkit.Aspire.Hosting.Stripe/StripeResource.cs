#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Stripe CLI container resource for local webhook forwarding and testing.
/// </summary>
/// <param name="name">The name of the resource.</param>
[AspireExport(ExposeProperties = true)]
public class StripeResource(string name) : ContainerResource(name)
{
    /// <summary>
    /// Gets the webhook signing secret retrieved from the Stripe CLI.
    /// </summary>
    public string? WebhookSigningSecret { get; internal set; }
}

#pragma warning restore ASPIREATS001 // AspireExport is experimental
