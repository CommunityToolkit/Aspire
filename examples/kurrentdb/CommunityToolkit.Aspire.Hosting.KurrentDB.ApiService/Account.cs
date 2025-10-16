using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Hosting.KurrentDB.ApiService;

public class Account
{
    public Guid Id { get; private set; }
    public string? Name { get; private set; }
    public decimal Balance { get; private set; }

    [JsonIgnore]
    public int Version { get; private set; } = -1;

    [NonSerialized]
    private readonly Queue<object> uncommittedEvents = new();

    public static Account Create(Guid id, string name)
        => new(id, name);

    public void Deposit(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(amount, 0, nameof(amount));

        var @event = new AccountFundsDeposited(Id, amount);

        uncommittedEvents.Enqueue(@event);
        Apply(@event);
    }

    public void Withdraw(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(amount, 0, nameof(amount));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(amount, Balance, nameof(amount));

        var @event = new AccountFundsWithdrew(Id, amount);

        uncommittedEvents.Enqueue(@event);
        Apply(@event);
    }

    public void When(object @event)
    {
        switch (@event)
        {
            case AccountCreated accountCreated:
                Apply(accountCreated);
                break;
            case AccountFundsDeposited accountFundsDeposited:
                Apply(accountFundsDeposited);
                break;
            case AccountFundsWithdrew accountFundsWithdrew:
                Apply(accountFundsWithdrew);
                break;
        }
    }

    public object[] DequeueUncommittedEvents()
    {
        var dequeuedEvents = uncommittedEvents.ToArray();

        uncommittedEvents.Clear();

        return dequeuedEvents;
    }

    private Account()
    {
    }

    private Account(Guid id, string name)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        var @event = new AccountCreated(id, name);

        uncommittedEvents.Enqueue(@event);
        Apply(@event);
    }

    private void Apply(AccountCreated @event)
    {
        Version++;

        Id = @event.Id;
        Name = @event.Name;
    }

    private void Apply(AccountFundsDeposited @event)
    {
        Version++;

        Balance += @event.Amount;
    }

    private void Apply(AccountFundsWithdrew @event)
    {
        Version++;

        Balance -= @event.Amount;
    }
}
