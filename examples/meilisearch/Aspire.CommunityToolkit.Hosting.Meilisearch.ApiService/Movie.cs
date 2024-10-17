namespace Aspire.CommunityToolkit.Hosting.Meilisearch.ApiService;
public class Movie
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public IEnumerable<string>? Genres { get; set; }
}