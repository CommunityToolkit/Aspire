using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal sealed record VercelProjectOptionsAnnotation(string ProjectName) : IResourceAnnotation;
