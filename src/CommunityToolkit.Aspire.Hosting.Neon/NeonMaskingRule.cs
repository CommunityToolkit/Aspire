namespace CommunityToolkit.Aspire.Hosting.Neon;

/// <summary>
/// Represents a data masking rule for Neon anonymized branches.
/// </summary>
public sealed class NeonMaskingRule
{
    /// <summary>
    /// Gets or sets the name of the database containing the column to mask.
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the schema name containing the table.
    /// </summary>
    public string SchemaName { get; set; } = "public";

    /// <summary>
    /// Gets or sets the table name containing the column.
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the column name to mask.
    /// </summary>
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the PostgreSQL Anonymizer masking function to apply (e.g., <c>anon.fake_email()</c>).
    /// </summary>
    /// <remarks>
    /// Provide either <see cref="MaskingFunction"/> or <see cref="MaskingValue"/>, not both.
    /// </remarks>
    public string? MaskingFunction { get; set; }

    /// <summary>
    /// Gets or sets a static masking value to use instead of a masking function.
    /// </summary>
    /// <remarks>
    /// Provide either <see cref="MaskingFunction"/> or <see cref="MaskingValue"/>, not both.
    /// </remarks>
    public string? MaskingValue { get; set; }
}
