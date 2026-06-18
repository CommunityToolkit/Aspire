using Bogus;

namespace CommunityToolkit.Aspire.Hosting.SurrealDb.ApiService.Models;

/// <summary>
/// Faker test class to generate fake <see cref="Todo"/> objects.
/// </summary>
public class TodoFaker : Faker<Todo>
{
    public TodoFaker()
    {
        RuleFor(o => o.Title, f => f.Lorem.Sentence());
        RuleFor(o => o.DueBy, f => f.Date.FutureDateOnly());
        RuleFor(o => o.IsComplete, f => f.Random.Bool());
    }
}