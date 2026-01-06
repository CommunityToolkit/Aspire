using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Sftp;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding an SFTP resource to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class SftpHostingExtensions
{
    /// <summary>
    /// Adds atmoz SFTP to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="port">The SFTP port number for the atmoz SFTP container.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an SFTP container to the application model with users configuration in the specified file.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// builder.AddSftp("sftp");
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<SftpContainerResource> AddSftp(this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull("Service name must be specified.", nameof(name));
        SftpContainerResource resource = new(name);

        var resourceBuilder = builder.AddResource(resource)
            .WithImage(SftpContainerImageTags.Image)
            .WithImageTag(SftpContainerImageTags.Tag)
            .WithImageRegistry(SftpContainerImageTags.Registry)
            .WithEndpoint("sftp", ep =>
            {
                ep.Port = port;
                ep.TargetPort = SftpContainerResource.SftpEndpointPort;
                ep.UriScheme = "sftp";
            });

        return resourceBuilder;
    }

    /// <summary>
    /// Adds a bind mount for the users.conf file to an SFTP container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="usersFile">The path to the users.conf file.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an SFTP container to the application model with users configuration in the specified file.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// builder.AddSftp("sftp").WithUsersFile("./etc/sftp/users.conf");
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<SftpContainerResource> WithUsersFile(this IResourceBuilder<SftpContainerResource> builder, string usersFile)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(usersFile, nameof(usersFile));

        var fileInfo = new FileInfo(usersFile);

        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException($"File '{fileInfo.FullName}' not found");
        }

        return builder.WithBindMount(fileInfo.FullName, "/etc/sftp/users.conf", isReadOnly: true);
    }

    /// <summary>
    /// Adds a bind mount for the specified host key file to an SFTP container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="keyFile">The path to the host key file.</param>
    /// <param name="keyType">The type of the host key.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an SFTP container to the application model with the host key in the specified file.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// builder.AddSftp("sftp").WithHostKeyFile("./etc/ssh/ssh_host_ed25519_key", KeyType.Ed25519);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<SftpContainerResource> WithHostKeyFile(this IResourceBuilder<SftpContainerResource> builder, string keyFile, KeyType keyType)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(keyFile, nameof(keyFile));

        var fileInfo = new FileInfo(keyFile);

        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException($"File '{fileInfo.FullName}' not found");
        }

        switch (keyType)
        {
            case KeyType.Ed25519:
                builder.WithBindMount(fileInfo.FullName, "/etc/ssh/ssh_host_ed25519_key");
                break;

            case KeyType.Rsa:
                builder.WithBindMount(fileInfo.FullName, "/etc/ssh/ssh_host_rsa_key");
                break;
        }

        return builder;
    }

    /// <summary>
    /// Adds a bind mount for the public key file of the specified user to an SFTP container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="username">The user whose public key is being bind mounted</param>
    /// <param name="keyFile">The public key file of the specified user (will be bind mounted on the server).</param>
    /// <param name="keyType">The type of the host key.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an SFTP container to the application model with the host key in the specified file.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// builder.AddSftp("sftp").WithUserKeyFile("foo", "./home/foo/.ssh/keys/id_rsa.pub", KeyType.Rsa);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<SftpContainerResource> WithUserKeyFile(this IResourceBuilder<SftpContainerResource> builder, string username, string keyFile, KeyType keyType)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(username, nameof(username));
        ArgumentNullException.ThrowIfNullOrEmpty(keyFile, nameof(keyFile));

        var fileInfo = new FileInfo(keyFile);

        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException($"File '{fileInfo.FullName}' not found");
        }

        return builder.WithBindMount(fileInfo.FullName, $"/home/{username}/.ssh/keys/{fileInfo.Name}");
    }
}
