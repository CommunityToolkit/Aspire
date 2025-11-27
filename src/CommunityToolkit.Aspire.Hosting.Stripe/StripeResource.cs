namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Stripe CLI resource for local webhook forwarding and testing.
/// </summary>
/// <param name="name">The name of the resource.</param>
public class StripeResource(string name) : ExecutableResource(name, "stripe", "");
