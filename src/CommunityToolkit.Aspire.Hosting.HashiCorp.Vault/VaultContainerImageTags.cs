namespace CommunityToolkit.Aspire.Hosting.HashiCorp.Vault;

/// <summary>
/// Provides constants that represent Docker container image details
/// for the HashiCorp Vault service. These constants include the container
/// registry, image name, and tag version used for the Vault container.
/// </summary>
public static class VaultContainerImageTags
{
    /// <summary>
    /// Represents the container registry URL used for the HashiCorp Vault
    /// Docker container image. This constant is utilized to specify the registry
    /// from which the Vault container image is fetched.
    /// </summary>
    public const string Registry = "docker.io";

    /// <summary>
    /// Represents the Docker image name for the HashiCorp Vault container.
    /// This constant is used to specify the image identifier required
    /// to pull and run the Vault container from the Docker registry.
    /// </summary>
    public const string Image = "hashicorp/vault";

    /// <summary>
    /// Specifies the version tag for the HashiCorp Vault Docker container image.
    /// This constant is used to indicate the specific version of the Vault image
    /// that is being referenced in a containerized environment.
    /// </summary>
    public const string Tag = "1.19";
}