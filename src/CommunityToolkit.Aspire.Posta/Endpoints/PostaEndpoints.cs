namespace CommunityToolkit.Aspire.Posta.Endpoints;

/// <summary>
/// Provides the default application-facing Posta API endpoint definitions.
/// </summary>
public class PostaEndpoints : IPostaEndpoints
{
    /// <inheritdoc />
    public virtual PostaEndpoint SendAnEmail { get; } = new(HttpMethod.Post, "/api/v1/emails/send", PostaAuthentication.ApiKey);
    /// <inheritdoc />
    public virtual PostaEndpoint SendBatchEmails { get; } = new(HttpMethod.Post, "/api/v1/emails/batch", PostaAuthentication.ApiKey);
    /// <inheritdoc />
    public virtual PostaEndpoint SendEmailUsingTemplate { get; } = new(HttpMethod.Post, "/api/v1/emails/send-template", PostaAuthentication.ApiKey);
    /// <inheritdoc />
    public virtual PostaEndpoint PreviewEmailFromTemplate { get; } = new(HttpMethod.Post, "/api/v1/emails/preview", PostaAuthentication.ApiKey);
    /// <inheritdoc />
    public virtual PostaEndpoint VerifyAnEmailAddress { get; } = new(HttpMethod.Post, "/api/v1/emails/verify", PostaAuthentication.ApiKey);
    /// <inheritdoc />
    public virtual PostaEndpoint GetEmailDetails { get; } = new(HttpMethod.Get, "/api/v1/emails/{id}", PostaAuthentication.ApiKey);
    /// <inheritdoc />
    public virtual PostaEndpoint GetEmailDeliveryStatus { get; } = new(HttpMethod.Get, "/api/v1/emails/{id}/status", PostaAuthentication.ApiKey);
    /// <inheritdoc />
    public virtual PostaEndpoint RetryFailedEmail { get; } = new(HttpMethod.Post, "/api/v1/emails/{id}/retry", PostaAuthentication.ApiKey);
    /// <inheritdoc />
    public virtual PostaEndpoint ListTemplates { get; } = new(HttpMethod.Get, "/api/v1/workspaces/current/templates", PostaAuthentication.AccessToken);
    /// <inheritdoc />
    public virtual PostaEndpoint CreateTemplate { get; } = new(HttpMethod.Post, "/api/v1/workspaces/current/templates", PostaAuthentication.AccessToken);
    /// <inheritdoc />
    public virtual PostaEndpoint GetTemplate { get; } = new(HttpMethod.Get, "/api/v1/workspaces/current/templates/{id}", PostaAuthentication.AccessToken);
    /// <inheritdoc />
    public virtual PostaEndpoint UpdateTemplate { get; } = new(HttpMethod.Put, "/api/v1/workspaces/current/templates/{id}", PostaAuthentication.AccessToken);
    /// <inheritdoc />
    public virtual PostaEndpoint DeleteTemplate { get; } = new(HttpMethod.Delete, "/api/v1/workspaces/current/templates/{id}", PostaAuthentication.AccessToken);
    /// <inheritdoc />
    public virtual PostaEndpoint PreviewTemplate { get; } = new(HttpMethod.Post, "/api/v1/workspaces/current/templates/preview", PostaAuthentication.AccessToken);
    /// <inheritdoc />
    public virtual PostaEndpoint SendTestEmail { get; } = new(HttpMethod.Post, "/api/v1/workspaces/current/templates/{id}/send-test", PostaAuthentication.AccessToken);
    /// <inheritdoc />
    public virtual PostaEndpoint SubscribeAnEmailToAList { get; } = new(HttpMethod.Post, "/api/v1/subscriber-lists/subscribe", PostaAuthentication.ApiKey);
    /// <inheritdoc />
    public virtual PostaEndpoint UnsubscribeAnEmailFromAList { get; } = new(HttpMethod.Post, "/api/v1/subscriber-lists/{id}/unsubscribe", PostaAuthentication.ApiKey);
    /// <inheritdoc />
    public virtual PostaEndpoint ReSubscribeAnEmailToAList { get; } = new(HttpMethod.Post, "/api/v1/subscriber-lists/{id}/resubscribe", PostaAuthentication.ApiKey);
    /// <inheritdoc />
    public virtual PostaEndpoint LivenessProbe { get; } = new(HttpMethod.Get, "/healthz", PostaAuthentication.None);
    /// <inheritdoc />
    public virtual PostaEndpoint ReadinessProbe { get; } = new(HttpMethod.Get, "/readyz", PostaAuthentication.None);
    /// <inheritdoc />
    public virtual PostaEndpoint ApplicationInfo { get; } = new(HttpMethod.Get, "/api/v1/info", PostaAuthentication.None);
}