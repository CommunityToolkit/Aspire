using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Shared;

/// <summary>Represents the ListTemplatesResponsePageable payload.</summary>
public class ListTemplatesResponsePageable
{
    /// <summary>Gets or sets <c>current_page</c>.</summary>
    [JsonPropertyName("current_page")]
    public int? CurrentPage { get; set; }

    /// <summary>Gets or sets <c>empty</c>.</summary>
    [JsonPropertyName("empty")]
    public bool? Empty { get; set; }

    /// <summary>Gets or sets <c>size</c>.</summary>
    [JsonPropertyName("size")]
    public int? Size { get; set; }

    /// <summary>Gets or sets <c>total_elements</c>.</summary>
    [JsonPropertyName("total_elements")]
    public long? TotalElements { get; set; }

    /// <summary>Gets or sets <c>total_pages</c>.</summary>
    [JsonPropertyName("total_pages")]
    public int? TotalPages { get; set; }

}