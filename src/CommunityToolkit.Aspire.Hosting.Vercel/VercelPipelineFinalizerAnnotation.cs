using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

/// <summary>
/// Marks resources that need a late pipeline normalization pass after Vercel project options
/// are attached to individual workload resources.
/// </summary>
internal sealed class VercelPipelineFinalizerAnnotation : IResourceAnnotation;
