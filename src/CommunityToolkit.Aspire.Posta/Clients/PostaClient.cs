using CommunityToolkit.Aspire.Posta.Endpoints;
using CommunityToolkit.Aspire.Posta.Transport;

namespace CommunityToolkit.Aspire.Posta.Clients;

internal sealed class PostaClient(PostaTransport transport, IPostaEndpoints endpoints) : IPostaClient
{
    private readonly PostaClientSection _section = new(transport, endpoints);

    public IPostaEndpoints Endpoints { get; } = endpoints;
    public IPostaEmailsClient Emails => _section;
    public IPostaTemplatesClient Templates => _section;
    public IPostaSubscriberListsClient SubscriberLists => _section;
    public IPostaHealthClient Health => _section;
    public IPostaInfoClient Info => _section;
}
