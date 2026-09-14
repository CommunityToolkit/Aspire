namespace Aspire.Hosting;

/// <summary>
/// Configuration options for N8n metrics collection.
/// </summary>
/// <remarks>
/// All metric categories are enabled by default. Set individual properties to <see langword="false"/> to disable specific metric types.
/// </remarks>
[AspireExport(ExposeProperties = true)]
public class N8nMetricOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to include webhook-related metrics.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to include webhook metrics; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
    /// </value>
    /// <remarks>
    /// Maps to the <c>N8N_METRICS_INCLUDE_WEBHOOK_METRICS</c> environment variable.
    /// </remarks>
    public bool IncludeWebhookMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to include workflow information in metrics.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to include workflow info; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
    /// </value>
    /// <remarks>
    /// Maps to the <c>N8N_METRICS_INCLUDE_WORKFLOW_INFO</c> environment variable.
    /// </remarks>
    public bool IncludeWorkflowInfo { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to include form-related metrics.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to include form metrics; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
    /// </value>
    /// <remarks>
    /// Maps to the <c>N8N_METRICS_INCLUDE_FORM_METRICS</c> environment variable.
    /// </remarks>
    public bool IncludeFormMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to include workflow ID labels in metrics.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to include workflow ID labels; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
    /// </value>
    /// <remarks>
    /// Maps to the <c>N8N_METRICS_INCLUDE_WORKFLOW_ID_LABEL</c> environment variable.
    /// </remarks>
    public bool IncludeWorkflowIdLabel { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to include node type labels in metrics.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to include node type labels; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
    /// </value>
    /// <remarks>
    /// Maps to the <c>N8N_METRICS_INCLUDE_NODE_TYPE_LABEL</c> environment variable.
    /// </remarks>
    public bool IncludeNodeTypeLabel { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to include credential type labels in metrics.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to include credential type labels; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
    /// </value>
    /// <remarks>
    /// Maps to the <c>N8N_METRICS_INCLUDE_CREDENTIAL_TYPE_LABEL</c> environment variable.
    /// </remarks>
    public bool IncludeCredentialTypeLabel { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to include API endpoint metrics.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to include API endpoint metrics; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
    /// </value>
    /// <remarks>
    /// Maps to the <c>N8N_METRICS_INCLUDE_API_ENDPOINTS</c> environment variable.
    /// </remarks>
    public bool IncludeApiEndpoints { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to include API path labels in metrics.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to include API path labels; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
    /// </value>
    /// <remarks>
    /// Maps to the <c>N8N_METRICS_INCLUDE_API_PATH_LABEL</c> environment variable.
    /// </remarks>
    public bool IncludeApiPathLabel { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to include queue-related metrics.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to include queue metrics; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
    /// </value>
    /// <remarks>
    /// Maps to the <c>N8N_METRICS_INCLUDE_QUEUE_METRICS</c> environment variable.
    /// </remarks>
    public bool IncludeQueueMetrics { get; set; } = true;
}