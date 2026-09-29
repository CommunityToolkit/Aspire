using CommunityToolkit.Aspire.Posta.Endpoints;

namespace CommunityToolkit.Aspire.Posta.Clients;

/// <summary>
/// Provides access to all Posta API sections and endpoint definitions.
/// </summary>
public interface IPostaClient
{
    /// <summary>Gets the overridable endpoint catalog.</summary>
    IPostaEndpoints Endpoints { get; }
    /// <summary>Gets email operations.</summary>
    IPostaEmailsClient Emails { get; }
    /// <summary>Gets template operations.</summary>
    IPostaTemplatesClient Templates { get; }
    /// <summary>Gets subscriber-list operations.</summary>
    IPostaSubscriberListsClient SubscriberLists { get; }
    /// <summary>Gets health operations.</summary>
    IPostaHealthClient Health { get; }
    /// <summary>Gets server information operations.</summary>
    IPostaInfoClient Info { get; }
}