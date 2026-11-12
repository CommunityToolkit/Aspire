using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.RavenDB;

/// <summary>
/// The settings of the RavenDB client integration (CommunityToolkit.Aspire.RavenDB.Client) that a deployment package
/// fills in for an application. The client reads them from <c>Aspire:RavenDB:Client</c> and from
/// <c>Aspire:RavenDB:Client:{connection name}</c>.
/// </summary>
internal static class RavenDBClientConfiguration
{
    /// <summary>The client certificate as base64 PKCS #12 (pfx) content.</summary>
    public const string CertificateSetting = "CertificateBase64";

    /// <summary>The environment variable of <paramref name="setting"/>, for one connection or for all of them.</summary>
    public static string Variable(string? connectionName, string setting) =>
        connectionName is null
            ? $"Aspire__RavenDB__Client__{setting}"
            : $"Aspire__RavenDB__Client__{connectionName}__{setting}";

    /// <summary>
    /// Gives the application its client certificate as a value, such as one only known at deploy time, which each
    /// compute environment delivers its own way (Docker Compose through its .env file).
    /// </summary>
    /// <remarks>
    /// The value goes under every connection name the application references the server by. When it uses no other
    /// RavenDB server it also goes to the default section, which a connection renamed with
    /// <c>WithReference(db, connectionName: ...)</c> still reads: Aspire does not expose that name.
    /// </remarks>
    public static void DeliverCertificate(RavenDBConsumer consumer, object certificate)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        ArgumentNullException.ThrowIfNull(certificate);

        var variables = consumer.ConnectionNames.Select(name => Variable(name, CertificateSetting)).ToList();

        if (consumer.ServersReferenced == 1)
        {
            variables.Add(Variable(null, CertificateSetting));
        }

        consumer.Resource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            foreach (var variable in variables)
            {
                context.EnvironmentVariables[variable] = certificate;
            }
        }));
    }
}
