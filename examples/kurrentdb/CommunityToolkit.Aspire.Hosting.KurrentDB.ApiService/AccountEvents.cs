namespace CommunityToolkit.Aspire.Hosting.KurrentDB.ApiService;

public record AccountCreated(Guid Id, string Name);

public record AccountFundsDeposited(Guid Id, decimal Amount);

public record AccountFundsWithdrew(Guid Id, decimal Amount);
