using CommunityToolkit.Aspire.Posta.Endpoints;
using CommunityToolkit.Aspire.Posta.Transport;

namespace CommunityToolkit.Aspire.Posta.Clients;

internal sealed class PostaClient : IPostaClient
{
    private readonly PostaClientSection _section;

    internal PostaClient(PostaTransport transport, IPostaEndpoints endpoints)
    {
        _section = new PostaClientSection(transport, endpoints);
        Endpoints = endpoints;
    }

    public IPostaEndpoints Endpoints { get; }
    public IPostaEmailsClient Emails => _section;
    public IPostaTemplatesClient Templates => _section;
    public IPostaSubscriberListsClient SubscriberLists => _section;
    public IPostaHealthClient Health => _section;
    public IPostaInfoClient Info => _section;
}