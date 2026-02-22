using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Neon;

internal sealed record NeonExternalProvisionerAnnotation(
	IResourceWithWaitSupport Resource,
	string ProjectPath,
	string OutputFilePath,
	NeonProvisionerMode Mode) : IResourceAnnotation;
