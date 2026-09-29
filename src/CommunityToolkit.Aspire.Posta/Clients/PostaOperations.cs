using CommunityToolkit.Aspire.Posta.Endpoints;
using CommunityToolkit.Aspire.Posta.Transport;
using Emails = CommunityToolkit.Aspire.Posta.Models.Emails;
using Health = CommunityToolkit.Aspire.Posta.Models.Health;
using Info = CommunityToolkit.Aspire.Posta.Models.Info;
using SubscriberLists = CommunityToolkit.Aspire.Posta.Models.SubscriberLists;
using Templates = CommunityToolkit.Aspire.Posta.Models.Templates;

namespace CommunityToolkit.Aspire.Posta.Clients;

/// <summary>Provides Posta emails operations.</summary>
public interface IPostaEmailsClient : IPostaSectionClient
{
    /// <summary>Invokes the SendBatchEmails operation.</summary>
    Task<Emails.SendBatchEmailsResponse?> SendBatchEmailsAsync(Emails.SendBatchEmailsRequest request, CancellationToken cancellationToken = default);

    /// <summary>Invokes the PreviewEmailFromTemplate operation.</summary>
    Task<Emails.PreviewEmailFromTemplateResponse?> PreviewEmailFromTemplateAsync(Emails.PreviewEmailFromTemplateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Invokes the SendAnEmail operation.</summary>
    Task<Emails.SendAnEmailResponse?> SendAnEmailAsync(Emails.SendAnEmailRequest request, CancellationToken cancellationToken = default);

    /// <summary>Invokes the SendEmailUsingTemplate operation.</summary>
    Task<Emails.SendEmailUsingTemplateResponse?> SendEmailUsingTemplateAsync(Emails.SendEmailUsingTemplateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Invokes the VerifyAnEmailAddress operation.</summary>
    Task<Emails.VerifyAnEmailAddressResponse?> VerifyAnEmailAddressAsync(Emails.VerifyAnEmailAddressRequest request, CancellationToken cancellationToken = default);

    /// <summary>Invokes the GetEmailDetails operation.</summary>
    Task<Emails.GetEmailDetailsResponse?> GetEmailDetailsAsync(Emails.GetEmailDetailsRequest request, CancellationToken cancellationToken = default);

    /// <summary>Invokes the RetryFailedEmail operation.</summary>
    Task<Emails.RetryFailedEmailResponse?> RetryFailedEmailAsync(Emails.RetryFailedEmailRequest request, CancellationToken cancellationToken = default);

    /// <summary>Invokes the GetEmailDeliveryStatus operation.</summary>
    Task<Emails.GetEmailDeliveryStatusResponse?> GetEmailDeliveryStatusAsync(Emails.GetEmailDeliveryStatusRequest request, CancellationToken cancellationToken = default);

}

/// <summary>Provides Posta templates operations.</summary>
public interface IPostaTemplatesClient : IPostaSectionClient
{
    /// <summary>Invokes the ListTemplates operation.</summary>
    Task<Templates.ListTemplatesResponse?> ListTemplatesAsync(Templates.ListTemplatesRequest? request = null, CancellationToken cancellationToken = default);

    /// <summary>Invokes the CreateTemplate operation.</summary>
    Task<Templates.CreateTemplateResponse?> CreateTemplateAsync(Templates.CreateTemplateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Invokes the PreviewTemplate operation.</summary>
    Task<Templates.PreviewTemplateResponse?> PreviewTemplateAsync(Templates.PreviewTemplateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Invokes the DeleteTemplate operation.</summary>
    Task<Templates.DeleteTemplateResponse?> DeleteTemplateAsync(Templates.DeleteTemplateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Invokes the GetTemplate operation.</summary>
    Task<Templates.GetTemplateResponse?> GetTemplateAsync(Templates.GetTemplateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Invokes the UpdateTemplate operation.</summary>
    Task<Templates.UpdateTemplateResponse?> UpdateTemplateAsync(Templates.UpdateTemplateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Invokes the SendTestEmail operation.</summary>
    Task<Templates.SendTestEmailResponse?> SendTestEmailAsync(Templates.SendTestEmailRequest request, CancellationToken cancellationToken = default);

}

/// <summary>Provides Posta subscriberlists operations.</summary>
public interface IPostaSubscriberListsClient : IPostaSectionClient
{
    /// <summary>Invokes the SubscribeAnEmailToAList operation.</summary>
    Task<SubscriberLists.SubscribeAnEmailToAListResponse?> SubscribeAnEmailToAListAsync(SubscriberLists.SubscribeAnEmailToAListRequest request, CancellationToken cancellationToken = default);

    /// <summary>Invokes the ReSubscribeAnEmailToAList operation.</summary>
    Task<SubscriberLists.ReSubscribeAnEmailToAListResponse?> ReSubscribeAnEmailToAListAsync(SubscriberLists.ReSubscribeAnEmailToAListRequest request, CancellationToken cancellationToken = default);

    /// <summary>Invokes the UnsubscribeAnEmailFromAList operation.</summary>
    Task<SubscriberLists.UnsubscribeAnEmailFromAListResponse?> UnsubscribeAnEmailFromAListAsync(SubscriberLists.UnsubscribeAnEmailFromAListRequest request, CancellationToken cancellationToken = default);

}

/// <summary>Provides Posta health operations.</summary>
public interface IPostaHealthClient : IPostaSectionClient
{
    /// <summary>Invokes the LivenessProbe operation.</summary>
    Task<Health.LivenessProbeResponse?> LivenessProbeAsync(CancellationToken cancellationToken = default);

    /// <summary>Invokes the ReadinessProbe operation.</summary>
    Task<Health.ReadinessProbeResponse?> ReadinessProbeAsync(CancellationToken cancellationToken = default);

}

/// <summary>Provides Posta info operations.</summary>
public interface IPostaInfoClient : IPostaSectionClient
{
    /// <summary>Invokes the ApplicationInfo operation.</summary>
    Task<Info.ApplicationInfoResponse?> ApplicationInfoAsync(CancellationToken cancellationToken = default);

}

internal sealed partial class PostaClientSection
{
    public Task<Emails.SendBatchEmailsResponse?> SendBatchEmailsAsync(Emails.SendBatchEmailsRequest request, CancellationToken cancellationToken = default)
    {
        PostaRequest postaRequest = new()
        {
            Body = request,
            Query = new Dictionary<string, object?>
            {
                ["dry_run"] = request?.DryRun,
            },
        };

        return SendAsync<Emails.SendBatchEmailsResponse>(_endpoints.SendBatchEmails, postaRequest, cancellationToken);
    }

    public Task<Emails.PreviewEmailFromTemplateResponse?> PreviewEmailFromTemplateAsync(Emails.PreviewEmailFromTemplateRequest request, CancellationToken cancellationToken = default)
    {
        PostaRequest postaRequest = new()
        {
            Body = request,
        };

        return SendAsync<Emails.PreviewEmailFromTemplateResponse>(_endpoints.PreviewEmailFromTemplate, postaRequest, cancellationToken);
    }

    public Task<Emails.SendAnEmailResponse?> SendAnEmailAsync(Emails.SendAnEmailRequest request, CancellationToken cancellationToken = default)
    {
        PostaRequest postaRequest = new()
        {
            Body = request,
            Query = new Dictionary<string, object?>
            {
                ["dry_run"] = request?.DryRun,
            },
        };

        return SendAsync<Emails.SendAnEmailResponse>(_endpoints.SendAnEmail, postaRequest, cancellationToken);
    }

    public Task<Emails.SendEmailUsingTemplateResponse?> SendEmailUsingTemplateAsync(Emails.SendEmailUsingTemplateRequest request, CancellationToken cancellationToken = default)
    {
        PostaRequest postaRequest = new()
        {
            Body = request,
            Query = new Dictionary<string, object?>
            {
                ["dry_run"] = request?.DryRun,
            },
        };

        return SendAsync<Emails.SendEmailUsingTemplateResponse>(_endpoints.SendEmailUsingTemplate, postaRequest, cancellationToken);
    }

    public Task<Emails.VerifyAnEmailAddressResponse?> VerifyAnEmailAddressAsync(Emails.VerifyAnEmailAddressRequest request, CancellationToken cancellationToken = default)
    {
        PostaRequest postaRequest = new()
        {
            Body = request,
            Query = new Dictionary<string, object?>
            {
                ["fresh"] = request?.Fresh,
            },
        };

        return SendAsync<Emails.VerifyAnEmailAddressResponse>(_endpoints.VerifyAnEmailAddress, postaRequest, cancellationToken);
    }

    public Task<Emails.GetEmailDetailsResponse?> GetEmailDetailsAsync(Emails.GetEmailDetailsRequest request, CancellationToken cancellationToken = default)
    {
        PostaRequest postaRequest = new()
        {
            PathParameters = new Dictionary<string, object?>
            {
                ["id"] = request?.Id,
            },
        };

        return SendAsync<Emails.GetEmailDetailsResponse>(_endpoints.GetEmailDetails, postaRequest, cancellationToken);
    }

    public Task<Emails.RetryFailedEmailResponse?> RetryFailedEmailAsync(Emails.RetryFailedEmailRequest request, CancellationToken cancellationToken = default)
    {
        PostaRequest postaRequest = new()
        {
            PathParameters = new Dictionary<string, object?>
            {
                ["id"] = request?.Id,
            },
        };

        return SendAsync<Emails.RetryFailedEmailResponse>(_endpoints.RetryFailedEmail, postaRequest, cancellationToken);
    }

    public Task<Emails.GetEmailDeliveryStatusResponse?> GetEmailDeliveryStatusAsync(Emails.GetEmailDeliveryStatusRequest request, CancellationToken cancellationToken = default)
    {
        PostaRequest postaRequest = new()
        {
            PathParameters = new Dictionary<string, object?>
            {
                ["id"] = request?.Id,
            },
        };

        return SendAsync<Emails.GetEmailDeliveryStatusResponse>(_endpoints.GetEmailDeliveryStatus, postaRequest, cancellationToken);
    }

    public Task<Templates.ListTemplatesResponse?> ListTemplatesAsync(Templates.ListTemplatesRequest? request = null, CancellationToken cancellationToken = default)
    {
        PostaRequest postaRequest = new()
        {
            Query = new Dictionary<string, object?>
            {
                ["page"] = request?.Page,
                ["size"] = request?.Size,
                ["search"] = request?.Search,
            },
        };

        return SendAsync<Templates.ListTemplatesResponse>(_endpoints.ListTemplates, postaRequest, cancellationToken);
    }

    public Task<Templates.CreateTemplateResponse?> CreateTemplateAsync(Templates.CreateTemplateRequest request, CancellationToken cancellationToken = default)
    {
        PostaRequest postaRequest = new()
        {
            Body = request,
        };

        return SendAsync<Templates.CreateTemplateResponse>(_endpoints.CreateTemplate, postaRequest, cancellationToken);
    }

    public Task<Templates.PreviewTemplateResponse?> PreviewTemplateAsync(Templates.PreviewTemplateRequest request, CancellationToken cancellationToken = default)
    {
        PostaRequest postaRequest = new()
        {
            Body = request,
        };

        return SendAsync<Templates.PreviewTemplateResponse>(_endpoints.PreviewTemplate, postaRequest, cancellationToken);
    }

    public Task<Templates.DeleteTemplateResponse?> DeleteTemplateAsync(Templates.DeleteTemplateRequest request, CancellationToken cancellationToken = default)
    {
        PostaRequest postaRequest = new()
        {
            PathParameters = new Dictionary<string, object?>
            {
                ["id"] = request?.Id,
            },
        };

        return SendAsync<Templates.DeleteTemplateResponse>(_endpoints.DeleteTemplate, postaRequest, cancellationToken);
    }

    public Task<Templates.GetTemplateResponse?> GetTemplateAsync(Templates.GetTemplateRequest request, CancellationToken cancellationToken = default)
    {
        PostaRequest postaRequest = new()
        {
            PathParameters = new Dictionary<string, object?>
            {
                ["id"] = request?.Id,
            },
        };

        return SendAsync<Templates.GetTemplateResponse>(_endpoints.GetTemplate, postaRequest, cancellationToken);
    }

    public Task<Templates.UpdateTemplateResponse?> UpdateTemplateAsync(Templates.UpdateTemplateRequest request, CancellationToken cancellationToken = default)
    {
        PostaRequest postaRequest = new()
        {
            Body = request,
            PathParameters = new Dictionary<string, object?>
            {
                ["id"] = request?.Id,
            },
        };

        return SendAsync<Templates.UpdateTemplateResponse>(_endpoints.UpdateTemplate, postaRequest, cancellationToken);
    }

    public Task<Templates.SendTestEmailResponse?> SendTestEmailAsync(Templates.SendTestEmailRequest request, CancellationToken cancellationToken = default)
    {
        PostaRequest postaRequest = new()
        {
            Body = request,
            PathParameters = new Dictionary<string, object?>
            {
                ["id"] = request?.Id,
            },
        };

        return SendAsync<Templates.SendTestEmailResponse>(_endpoints.SendTestEmail, postaRequest, cancellationToken);
    }

    public Task<SubscriberLists.SubscribeAnEmailToAListResponse?> SubscribeAnEmailToAListAsync(SubscriberLists.SubscribeAnEmailToAListRequest request, CancellationToken cancellationToken = default)
    {
        PostaRequest postaRequest = new()
        {
            Body = request,
        };

        return SendAsync<SubscriberLists.SubscribeAnEmailToAListResponse>(_endpoints.SubscribeAnEmailToAList, postaRequest, cancellationToken);
    }

    public Task<SubscriberLists.ReSubscribeAnEmailToAListResponse?> ReSubscribeAnEmailToAListAsync(SubscriberLists.ReSubscribeAnEmailToAListRequest request, CancellationToken cancellationToken = default)
    {
        PostaRequest postaRequest = new()
        {
            Body = request,
            PathParameters = new Dictionary<string, object?>
            {
                ["id"] = request?.Id,
            },
        };

        return SendAsync<SubscriberLists.ReSubscribeAnEmailToAListResponse>(_endpoints.ReSubscribeAnEmailToAList, postaRequest, cancellationToken);
    }

    public Task<SubscriberLists.UnsubscribeAnEmailFromAListResponse?> UnsubscribeAnEmailFromAListAsync(SubscriberLists.UnsubscribeAnEmailFromAListRequest request, CancellationToken cancellationToken = default)
    {
        PostaRequest postaRequest = new()
        {
            Body = request,
            PathParameters = new Dictionary<string, object?>
            {
                ["id"] = request?.Id,
            },
        };

        return SendAsync<SubscriberLists.UnsubscribeAnEmailFromAListResponse>(_endpoints.UnsubscribeAnEmailFromAList, postaRequest, cancellationToken);
    }

    public Task<Health.LivenessProbeResponse?> LivenessProbeAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync<Health.LivenessProbeResponse>(_endpoints.LivenessProbe, null, cancellationToken);
    }

    public Task<Health.ReadinessProbeResponse?> ReadinessProbeAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync<Health.ReadinessProbeResponse>(_endpoints.ReadinessProbe, null, cancellationToken);
    }

    public Task<Info.ApplicationInfoResponse?> ApplicationInfoAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync<Info.ApplicationInfoResponse>(_endpoints.ApplicationInfo, null, cancellationToken);
    }

}