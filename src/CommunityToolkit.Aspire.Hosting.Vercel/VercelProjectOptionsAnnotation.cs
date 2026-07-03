using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

/// <summary>
/// Stores the user-specified Vercel project name for Aspire-managed projects when no
/// checked-in <c>.vercel/project.json</c> link owns the project identity.
/// </summary>
internal sealed record VercelProjectOptionsAnnotation(string ProjectName) : IResourceAnnotation;
