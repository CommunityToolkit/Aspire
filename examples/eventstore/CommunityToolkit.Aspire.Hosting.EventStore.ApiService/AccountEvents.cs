namespace CommunityToolkit.Aspire.Hosting.EventStore.ApiService;

public record AccountCreated(Guid Id, string Name);

public record AccountFundsDeposited(Guid Id, decimal Amount);

public record AccountFundsWithdrew(Guid Id, decimal Amount);
