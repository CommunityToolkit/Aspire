namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Describes a ngrok endpoint.
/// </summary>
/// <param name="EndpointName">A unique name for this endpoint's configuration.</param>
/// <param name="Url">The address you would like to use to forward traffic to your upstream service. Leave empty to get a randomly assigned address.</param>
/// <param name="Labels">An optional dictionary of labels to apply to the endpoint.</param>
public sealed record NgrokEndpoint(string EndpointName, string? Url, IDictionary<string, string>? Labels = null);