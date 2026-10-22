using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;
using Raven.Client.Exceptions;
using Raven.Client.Exceptions.Security;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using Raven.Client.ServerWide.Operations.Certificates;

namespace CommunityToolkit.Aspire.Hosting.RavenDB.Cloud;

/// <summary>
/// The server-side operations a deployment performs on a RavenDB Cloud product, authenticated with the product's
/// admin certificate. Behind an interface so the deployment logic can be tested without a server.
/// </summary>
internal interface IRavenDBServerAdministration : IDisposable
{
    /// <summary>Succeeds once the server answers an authenticated request.</summary>
    Task PingAsync(CancellationToken cancellationToken);

    Task<bool> DatabaseExistsAsync(string database, CancellationToken cancellationToken);

    /// <returns><see langword="false"/> when the database was created concurrently.</returns>
    Task<bool> CreateDatabaseAsync(string database, int replicationFactor, CancellationToken cancellationToken);

    /// <summary>The registered certificates whose name starts with <paramref name="namePrefix"/>.</summary>
    Task<IReadOnlyList<RegisteredCertificate>> GetCertificatesAsync(string namePrefix, CancellationToken cancellationToken);

    /// <summary>Creates a client certificate with <see cref="SecurityClearance.ValidUser"/> clearance.</summary>
    /// <returns>The bundle the server returns (a zip with the .pfx).</returns>
    Task<byte[]> CreateClientCertificateAsync(string name, IReadOnlyDictionary<string, DatabaseAccess> permissions, CancellationToken cancellationToken);

    /// <summary>Removes a certificate; one that is already gone is not an error.</summary>
    Task DeleteCertificateAsync(string thumbprint, CancellationToken cancellationToken);
}

/// <summary>A certificate the server trusts. Database names in <paramref name="Permissions"/> compare case-insensitively.</summary>
internal sealed record RegisteredCertificate(
    string Name,
    string Thumbprint,
    SecurityClearance Clearance,
    IReadOnlyDictionary<string, DatabaseAccess> Permissions);

/// <summary>Connects to a product's server.</summary>
internal interface IRavenDBServerAdministrationFactory
{
    /// <summary>
    /// Connects to the cluster of <paramref name="urls"/>, its nodes. <paramref name="certificate"/> stays the caller's
    /// and must outlive every connection: the RavenDB client caches its HTTP clients per certificate thumbprint, with the
    /// first instance.
    /// </summary>
    IRavenDBServerAdministration Create(IReadOnlyList<string> urls, X509Certificate2? certificate);
}

/// <summary>Connects with the RavenDB client.</summary>
internal sealed class RavenDBServerAdministrationFactory : IRavenDBServerAdministrationFactory
{
    /// <inheritdoc />
    public IRavenDBServerAdministration Create(IReadOnlyList<string> urls, X509Certificate2? certificate) =>
        new RavenDBServerAdministration(urls, certificate);
}

/// <remarks>
/// Uses the RavenDB client, so the server certificate is validated the way every RavenDB client validates it:
/// <c>RequestExecutor.RemoteCertificateValidationCallback</c> applies.
/// </remarks>
internal sealed class RavenDBServerAdministration : IRavenDBServerAdministration
{
    private readonly DocumentStore _store;

    public RavenDBServerAdministration(IReadOnlyList<string> urls, X509Certificate2? certificate)
    {
        _store = new DocumentStore
        {
            Urls = [.. urls],
            Certificate = certificate,
            // The certificate is the deployment's, shared by every connection: a store disposes its own by default.
            Conventions = new DocumentConventions { DisposeCertificate = false },
        };
        _store.Initialize();
    }

    private ServerOperationExecutor Server => _store.Maintenance.Server;

    /// <summary>
    /// Whether <paramref name="exception"/> means the server did not answer, which waiting can fix, rather than that it
    /// refused the connection or the request, which waiting cannot.
    /// </summary>
    public static bool IsUnreachable(Exception exception)
    {
        var all = Flatten(exception).ToList();

        // A rejected TLS handshake, a certificate that cannot be used, or one RavenDB refuses (its SecurityException).
        return !all.Any(e => e is System.Security.Authentication.AuthenticationException or CryptographicException or SecurityException) &&
            all.Any(e => e is HttpRequestException or SocketException or TimeoutException);
    }

    private static IEnumerable<Exception> Flatten(Exception exception)
    {
        yield return exception;

        IEnumerable<Exception> inner = exception switch
        {
            AggregateException aggregate => aggregate.InnerExceptions,
            { InnerException: { } single } => [single],
            _ => [],
        };

        foreach (var nested in inner.SelectMany(Flatten))
        {
            yield return nested;
        }
    }

    /// <inheritdoc />
    public async Task PingAsync(CancellationToken cancellationToken) =>
        await Server.SendAsync(new GetDatabaseNamesOperation(0, 1), cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<bool> DatabaseExistsAsync(string database, CancellationToken cancellationToken) =>
        await Server.SendAsync(new GetDatabaseRecordOperation(database), cancellationToken).ConfigureAwait(false) is not null;

    /// <inheritdoc />
    public async Task<bool> CreateDatabaseAsync(string database, int replicationFactor, CancellationToken cancellationToken)
    {
        try
        {
            await Server.SendAsync(new CreateDatabaseOperation(new DatabaseRecord(database), replicationFactor), cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (ConcurrencyException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RegisteredCertificate>> GetCertificatesAsync(string namePrefix, CancellationToken cancellationToken)
    {
        const int PageSize = 1024;
        var certificates = new List<RegisteredCertificate>();

        for (var start = 0; ; start += PageSize)
        {
            var page = await Server.SendAsync(new GetCertificatesOperation(start, PageSize), cancellationToken).ConfigureAwait(false);

            certificates.AddRange(page
                .Where(c => c.Name?.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase) == true)
                .Select(c => new RegisteredCertificate(
                    c.Name,
                    c.Thumbprint,
                    c.SecurityClearance,
                    // Database names are case-insensitive in RavenDB.
                    new Dictionary<string, DatabaseAccess>(c.Permissions ?? [], StringComparer.OrdinalIgnoreCase))));

            if (page.Length < PageSize)
            {
                return certificates;
            }
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> CreateClientCertificateAsync(string name, IReadOnlyDictionary<string, DatabaseAccess> permissions, CancellationToken cancellationToken)
    {
        var certificate = await Server.SendAsync(
            new CreateClientCertificateOperation(name, new Dictionary<string, DatabaseAccess>(permissions), SecurityClearance.ValidUser, password: null),
            cancellationToken).ConfigureAwait(false);

        return certificate.RawData;
    }

    /// <inheritdoc />
    public async Task DeleteCertificateAsync(string thumbprint, CancellationToken cancellationToken)
    {
        try
        {
            await Server.SendAsync(new DeleteCertificateOperation(thumbprint), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException && IsAlreadyGone(exception))
        {
            // Removed concurrently, or by hand in the Studio: the goal is reached.
        }
    }

    private static bool IsAlreadyGone(Exception exception) =>
        exception.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase);

    /// <summary>Disposes the document store; the certificate stays the caller's.</summary>
    public void Dispose()
    {
        _store.Dispose();
    }
}
