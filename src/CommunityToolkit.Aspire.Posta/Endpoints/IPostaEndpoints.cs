namespace CommunityToolkit.Aspire.Posta.Endpoints;

/// <summary>
/// Defines the application-facing Posta API endpoints supported by this client.
/// </summary>
public interface IPostaEndpoints
{
    /// <summary>Gets the send email endpoint.</summary>
    PostaEndpoint SendAnEmail { get; }
    /// <summary>Gets the batch email endpoint.</summary>
    PostaEndpoint SendBatchEmails { get; }
    /// <summary>Gets the send-template endpoint.</summary>
    PostaEndpoint SendEmailUsingTemplate { get; }
    /// <summary>Gets the email preview endpoint.</summary>
    PostaEndpoint PreviewEmailFromTemplate { get; }
    /// <summary>Gets the email verification endpoint.</summary>
    PostaEndpoint VerifyAnEmailAddress { get; }
    /// <summary>Gets the email details endpoint.</summary>
    PostaEndpoint GetEmailDetails { get; }
    /// <summary>Gets the email status endpoint.</summary>
    PostaEndpoint GetEmailDeliveryStatus { get; }
    /// <summary>Gets the email retry endpoint.</summary>
    PostaEndpoint RetryFailedEmail { get; }
    /// <summary>Gets the templates collection endpoint.</summary>
    PostaEndpoint ListTemplates { get; }
    /// <summary>Gets the create-template endpoint.</summary>
    PostaEndpoint CreateTemplate { get; }
    /// <summary>Gets the template details endpoint.</summary>
    PostaEndpoint GetTemplate { get; }
    /// <summary>Gets the update-template endpoint.</summary>
    PostaEndpoint UpdateTemplate { get; }
    /// <summary>Gets the delete-template endpoint.</summary>
    PostaEndpoint DeleteTemplate { get; }
    /// <summary>Gets the template preview endpoint.</summary>
    PostaEndpoint PreviewTemplate { get; }
    /// <summary>Gets the template test-send endpoint.</summary>
    PostaEndpoint SendTestEmail { get; }
    /// <summary>Gets the subscriber-list subscription endpoint.</summary>
    PostaEndpoint SubscribeAnEmailToAList { get; }
    /// <summary>Gets the subscriber-list unsubscribe endpoint.</summary>
    PostaEndpoint UnsubscribeAnEmailFromAList { get; }
    /// <summary>Gets the subscriber-list resubscribe endpoint.</summary>
    PostaEndpoint ReSubscribeAnEmailToAList { get; }
    /// <summary>Gets the liveness endpoint.</summary>
    PostaEndpoint LivenessProbe { get; }
    /// <summary>Gets the readiness endpoint.</summary>
    PostaEndpoint ReadinessProbe { get; }
    /// <summary>Gets the application information endpoint.</summary>
    PostaEndpoint ApplicationInfo { get; }
}