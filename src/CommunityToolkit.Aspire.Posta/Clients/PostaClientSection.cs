using CommunityToolkit.Aspire.Posta.Endpoints;
using CommunityToolkit.Aspire.Posta.Transport;

namespace CommunityToolkit.Aspire.Posta.Clients;

internal sealed partial class PostaClientSection : PostaSectionClient,
    IPostaEmailsClient, IPostaTemplatesClient, IPostaSubscriberListsClient, IPostaHealthClient, IPostaInfoClient
{
    private readonly IPostaEndpoints _endpoints;

    internal PostaClientSection(PostaTransport transport, IPostaEndpoints endpoints)
        : base(transport)
    {
        _endpoints = endpoints;
    }
}