using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Raven.Client.ServerWide.Operations.Certificates;

namespace CommunityToolkit.Aspire.Hosting.RavenDB.Cloud;

/// <summary>
/// The applications' client certificates on the product's server, reached with the client admin certificate.
/// </summary>
/// <remarks>
/// The server keeps no private keys, and a CI runner keeps no files between deployments, so a certificate cannot be
/// reused: every deployment issues each application a new one, hands it over as a value, and revokes the previous ones
/// once the applications run with the new ones.
/// </remarks>
internal sealed class RavenDBCloudCertificates(ILogger logger)
{
    /// <summary>Attempts to revoke a certificate before the deployment fails.</summary>
    internal const int RevokeAttempts = 3;

    /// <summary>Delay between two attempts to revoke a certificate; shortened in tests.</summary>
    internal TimeSpan RevokeRetryDelay { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Issues each application a certificate named after it, with admin access to the databases it references and to
    /// nothing else, and sets it as the application's value.
    /// </summary>
    public async Task IssueAsync(
        IRavenDBServerAdministration server,
        RavenDBCloudDeployment deployment,
        IReadOnlyList<RavenDBConsumer> consumers,
        CancellationToken cancellationToken)
    {
        foreach (var certificate in deployment.Certificates)
        {
            var databases = consumers.FirstOrDefault(c => ReferenceEquals(c.Resource, certificate.Consumer))?.Databases ?? [];
            var name = deployment.CertificateName(certificate.Consumer);
            var permissions = databases.ToDictionary(d => d, _ => DatabaseAccess.Admin, StringComparer.OrdinalIgnoreCase);

            if (databases.Count == 0)
            {
                logger.LogWarning(
                    "'{Resource}' references '{Server}', which declares no database, so its certificate grants access to no " +
                    "database. Declare the databases with AddDatabase(...).",
                    certificate.Consumer.Name,
                    deployment.Server.Name);
            }

            var bundle = await server.CreateClientCertificateAsync(name, permissions, cancellationToken).ConfigureAwait(false);
            var pfx = ExtractPfx(bundle)
                ?? throw new InvalidOperationException($"The certificate RavenDB issued for '{certificate.Consumer.Name}' contains no .pfx file.");

            certificate.Value = Convert.ToBase64String(pfx);
            certificate.Thumbprint = GetThumbprint(pfx);

            logger.LogInformation(
                "Issued client certificate '{Name}' ({Thumbprint}) to '{Resource}' with access to {Databases}.",
                name,
                certificate.Thumbprint,
                certificate.Consumer.Name,
                databases.Count == 0 ? "no database" : string.Join(", ", databases));
        }
    }

    /// <summary>
    /// Revokes this AppHost's certificates other than the ones issued by this deployment: the previous ones of every
    /// application, and those of applications that are gone. Any other certificate is left alone.
    /// </summary>
    public async Task RevokeReplacedAsync(IRavenDBServerAdministration server, RavenDBCloudDeployment deployment, CancellationToken cancellationToken)
    {
        var issued = deployment.Certificates
            .Select(c => c.Thumbprint)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var certificate in await GetOwnAsync(server, deployment, cancellationToken).ConfigureAwait(false))
        {
            if (!issued.Contains(certificate.Thumbprint))
            {
                await RevokeAsync(server, deployment, certificate, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Revokes every certificate of this AppHost, for a product that keeps running after destroy.</summary>
    public async Task RevokeAllAsync(IRavenDBServerAdministration server, RavenDBCloudDeployment deployment, CancellationToken cancellationToken)
    {
        foreach (var certificate in await GetOwnAsync(server, deployment, cancellationToken).ConfigureAwait(false))
        {
            await RevokeAsync(server, deployment, certificate, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The names of the integration's certificates on the product that this AppHost does not issue, and so never revokes.</summary>
    public static async Task<IReadOnlyList<string>> GetOtherClientsAsync(
        IRavenDBServerAdministration server,
        RavenDBCloudDeployment deployment,
        CancellationToken cancellationToken)
    {
        var all = await server.GetCertificatesAsync("aspire.", cancellationToken).ConfigureAwait(false);

        return [.. all.Where(c => !IsIssuedByThisAppHost(c, deployment)).Select(c => c.Name).Order(StringComparer.Ordinal)];
    }

    private static async Task<IReadOnlyList<RegisteredCertificate>> GetOwnAsync(
        IRavenDBServerAdministration server,
        RavenDBCloudDeployment deployment,
        CancellationToken cancellationToken)
    {
        var registered = await server.GetCertificatesAsync(deployment.CertificateNamePrefix, cancellationToken).ConfigureAwait(false);

        return [.. registered.Where(c => IsIssuedByThisAppHost(c, deployment))];
    }

    /// <summary>
    /// Whether this AppHost issued <paramref name="certificate"/>: named as it names its certificates, with ValidUser
    /// clearance and Admin on databases the server declares, nothing more. A certificate that only shares the name, such
    /// as an Operator one made by hand in the Studio, is never revoked.
    /// </summary>
    internal static bool IsIssuedByThisAppHost(RegisteredCertificate certificate, RavenDBCloudDeployment deployment) =>
        deployment.IsOwnCertificate(certificate.Name) &&
        certificate.Clearance == SecurityClearance.ValidUser &&
        certificate.Permissions.All(p =>
            p.Value == DatabaseAccess.Admin &&
            deployment.Server.Databases.Values.Contains(p.Key, StringComparer.OrdinalIgnoreCase));

    private async Task RevokeAsync(
        IRavenDBServerAdministration server,
        RavenDBCloudDeployment deployment,
        RegisteredCertificate certificate,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await server.DeleteCertificateAsync(certificate.Thumbprint, cancellationToken).ConfigureAwait(false);
                logger.LogInformation("Revoked client certificate '{Name}' ({Thumbprint}).", certificate.Name, certificate.Thumbprint);
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                if (attempt >= RevokeAttempts)
                {
                    // A certificate left valid keeps its access: the deployment must not look successful.
                    throw new InvalidOperationException(
                        $"Could not revoke client certificate '{certificate.Name}' ({certificate.Thumbprint}) of RavenDB Cloud product " +
                        $"'{deployment.ProductName}' after {RevokeAttempts} attempts: {exception.Message} Revoke it in the RavenDB Studio " +
                        "(Manage Server, Certificates) or deploy again.",
                        exception);
                }

                logger.LogWarning(
                    "Could not revoke client certificate '{Name}' ({Thumbprint}), attempt {Attempt} of {Attempts}: {Message}",
                    certificate.Name,
                    certificate.Thumbprint,
                    attempt,
                    RevokeAttempts,
                    exception.Message);

                await Task.Delay(RevokeRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>The certificate comes as a zip bundle (pfx and pem files) or as a bare pfx.</summary>
    public static byte[]? ExtractPfx(byte[] bundle)
    {
        if (bundle.Length < 4 || bundle[0] != 'P' || bundle[1] != 'K')
        {
            return bundle.Length == 0 ? null : bundle;
        }

        using var archive = new ZipArchive(new MemoryStream(bundle), ZipArchiveMode.Read);
        var entry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        return buffer.ToArray();
    }

    /// <summary>Loads a .pfx without a password, as RavenDB issues them.</summary>
    public static X509Certificate2 Load(byte[] pfx) =>
#if NET9_0_OR_GREATER
        X509CertificateLoader.LoadPkcs12(pfx, password: null);
#else
        new(pfx, (string?)null);
#endif

    /// <summary>The thumbprint of the certificate in a .pfx.</summary>
    public static string GetThumbprint(byte[] pfx)
    {
        using var certificate = Load(pfx);
        return certificate.Thumbprint;
    }
}
