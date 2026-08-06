using CommunityToolkit.Aspire.Posta.Endpoints;
using CommunityToolkit.Aspire.Posta.Transport;

namespace CommunityToolkit.Aspire.Posta.Clients;

internal sealed partial class PostaClientSection(PostaTransport transport, IPostaEndpoints endpoints) : PostaSectionClient(transport),
    IPostaEmailsClient, IPostaTemplatesClient, IPostaSubscriberListsClient, IPostaHealthClient, IPostaInfoClient
{
    private readonly IPostaEndpoints _endpoints = endpoints;
}
