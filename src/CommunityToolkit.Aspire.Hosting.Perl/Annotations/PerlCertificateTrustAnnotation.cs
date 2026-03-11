using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Perl.Annotations;

/// <summary>
/// Marker annotation that prevents duplicate <c>WithPerlCertificateTrust</c> registrations.
/// </summary>
internal sealed class PerlCertificateTrustAnnotation : IResourceAnnotation;
