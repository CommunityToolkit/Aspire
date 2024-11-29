using EventStore.Client;
using System.Text.Json;
using System.Text;

namespace CommunityToolkit.Aspire.Hosting.EventStore.ApiService;

public static class EventStoreExtensions
{
    public static async Task<Account?> GetAccount(this EventStoreClient eventStore, Guid id, CancellationToken cancellationToken)
    {
        var readResult = eventStore.ReadStreamAsync(
            Direction.Forwards,
            $"account-{id:N}",
            StreamPosition.Start,
            cancellationToken: cancellationToken
        );

        var readState = await readResult.ReadState;
        if (readState == ReadState.StreamNotFound)
        {
            return null;
        }

        var account = (Account)Activator.CreateInstance(typeof(Account), true)!;

        await foreach (var resolvedEvent in readResult)
        {
            var @event = resolvedEvent.Deserialize();

            account.When(@event!);
        }

        return account;
    }

    public static async Task AppendAcountEvents(this EventStoreClient eventStore, Account account, CancellationToken cancellationToken)
    {
        var events = account.DequeueUncommittedEvents();

        var eventsToAppend = events
            .Select(@event => @event.Serialize()).ToArray();

        var expectedVersion = account.Version - events.Length;
        await eventStore.AppendToStreamAsync(
            $"account-{account.Id:N}",
            expectedVersion == 0 ? StreamRevision.None : StreamRevision.FromInt64(expectedVersion),
            eventsToAppend,
            cancellationToken: cancellationToken
        );
    }

    private static object? Deserialize(this ResolvedEvent resolvedEvent)
    {
        var eventClrTypeName = JsonDocument.Parse(resolvedEvent.Event.Metadata)
            .RootElement
            .GetProperty("EventClrTypeName")
            .GetString();

        return JsonSerializer.Deserialize(
            Encoding.UTF8.GetString(resolvedEvent.Event.Data.Span),
            Type.GetType(eventClrTypeName!)!);
    }

    private static EventData Serialize(this object @event)
    {
        return new EventData(
            Uuid.NewUuid(),
            @event.GetType().Name,
            data: Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event)),
            metadata: Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    { "EventClrTypeName", @event.GetType().AssemblyQualifiedName! }
                }))
        );
    }
}
