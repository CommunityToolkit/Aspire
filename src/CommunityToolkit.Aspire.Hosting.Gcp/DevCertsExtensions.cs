using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

#pragma warning disable ASPIRECERTIFICATES001

namespace CommunityToolkit.Aspire.Hosting.Gcp;

internal static class DevCertsExtensions
{
    internal static IResourceBuilder<T> WithDevCertificates<T>(this IResourceBuilder<T> builder, string devCertPath, string pemFileName, string keyFileName) where T : ContainerResource
    {
        return builder.WithContainerFiles(devCertPath, (context, ct) =>
        {
            var certificate = context.ServiceProvider.GetRequiredService<IDeveloperCertificateService>().Certificates[0];
            var certPem = PemEncoding.Write("CERTIFICATE", certificate.RawData);
            char[]? keyPem = null;

            using (var rsa = certificate.GetRSAPrivateKey())
            {
                if (rsa != null)
                {
                    var keyBytes = rsa.ExportPkcs8PrivateKey();
                    keyPem = PemEncoding.Write("PRIVATE KEY", keyBytes);
                    goto end;
                }
            }

            using (var ecdsa = certificate.GetECDsaPrivateKey())
            {
                if (ecdsa != null)
                {
                    var keyBytes = ecdsa.ExportPkcs8PrivateKey();
                    keyPem = PemEncoding.Write("PRIVATE KEY", keyBytes);
                }
            }
            end:
            if (keyPem is null)
            {
                throw new InvalidOperationException("Dev Certificate does not contain a private key.");
            }

            ContainerFileSystemItem[] files = [
                new ContainerFile
                {
                    Contents = new string(certPem),
                    Name = pemFileName
                },
                new ContainerFile
                {
                    Contents = new string(keyPem),
                    Name = keyFileName
                }
            ];
            return Task.FromResult<IEnumerable<ContainerFileSystemItem>>(files);
        });
    }
}
