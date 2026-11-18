using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

/// <summary>
/// Launches real Perl processes through DCP and performs HTTPS requests with HTTP::Tiny
/// (<c>SSL_CERT_FILE</c>), LWP (<c>PERL_LWP_SSL_CA_FILE</c>), and Mojo::UserAgent (<c>MOJO_CA_FILE</c>).
/// The local server's certificate is issued by a test-only authority that is reachable only through
/// Aspire's certificate trust configuration; the public endpoint is reachable only through system roots.
/// </summary>
[RequiresLinux]
public class CertificateTrustRuntimeTests
{
    private const string PublicUrl = "https://www.cpan.org/";
    private static readonly string[] Clients = ["http-tiny", "lwp", "mojo"];

    [Fact]
    public async Task DefaultScope_PerlClientsTrustCustomAuthorityAndPublicAuthorities()
    {
        var results = await RunProbeAsync(scope: null, includePublicTarget: true);

        AssertOutcomes(results, ("local", true), ("public", true));
    }

    [Fact]
    public async Task AppendScope_PerlClientsKeepPublicAuthorities()
    {
        var results = await RunProbeAsync(CertificateTrustScope.Append, includePublicTarget: true);

        AssertOutcomes(results, ("public", true));
    }

    [Fact]
    public async Task NoneScope_PerlClientsDoNotTrustCustomAuthority()
    {
        var results = await RunProbeAsync(CertificateTrustScope.None, includePublicTarget: false);

        AssertOutcomes(results, ("local", false));
    }

    private static async Task<Dictionary<(string Client, string Target), string>> RunProbeAsync(
        CertificateTrustScope? scope,
        bool includePublicTarget)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var tempDirectory = new TempDirectory();
        File.WriteAllText(Path.Combine(tempDirectory.Path, "tls_probe.pl"), ProbeScript);
        var resultsPath = Path.Combine(tempDirectory.Path, "results.txt");

        using var authority = CreateCertificateAuthority();
        await using var server = TlsTestServer.Start(CreateServerCertificate(authority));

        List<string> arguments = [resultsPath, $"local={server.Url}"];
        if (includePublicTarget)
        {
            arguments.Add($"public={PublicUrl}");
        }

        using var builder = TestDistributedApplicationBuilder.Create(options => options.TrustDeveloperCertificate = false);
        var authorities = builder.AddCertificateAuthorityCollection("test-ca")
            .WithCertificate(X509CertificateLoader.LoadCertificate(authority.RawData));
        var probe = builder.AddPerlScript("tls-probe", tempDirectory.Path, "tls_probe.pl")
            .WithCertificateAuthorityCollection(authorities)
            .WithArgs([.. arguments]);

        if (scope is { } explicitScope)
        {
            probe.WithCertificateTrustScope(explicitScope);
        }

        await using var app = builder.Build();
        await app.StartAsync(cancellationToken);

        var finished = await app.Services.GetRequiredService<ResourceNotificationService>()
            .WaitForResourceAsync(
                probe.Resource.Name,
                e => KnownResourceStates.TerminalStates.Contains(e.Snapshot.State?.Text),
                cancellationToken)
            .WaitAsync(TimeSpan.FromMinutes(2), cancellationToken);

        Assert.True(
            finished.Snapshot.ExitCode == 0 && File.Exists(resultsPath),
            $"Perl probe ended in state '{finished.Snapshot.State?.Text}' with exit code {finished.Snapshot.ExitCode}.");

        return File.ReadAllLines(resultsPath)
            .Select(line => line.Split(' ', 3))
            .ToDictionary(parts => (parts[0], parts[1]), parts => parts[2]);
    }

    private static void AssertOutcomes(
        Dictionary<(string Client, string Target), string> results,
        params (string Target, bool ExpectSuccess)[] expectations)
    {
        List<string> mismatches = [];
        foreach (var client in Clients)
        {
            foreach (var (target, expectSuccess) in expectations)
            {
                if (!results.TryGetValue((client, target), out var outcome))
                {
                    mismatches.Add($"{client} -> {target}: no result");
                }
                else if (!outcome.StartsWith(expectSuccess ? "ok" : "fail", StringComparison.Ordinal))
                {
                    mismatches.Add($"{client} -> {target}: expected {(expectSuccess ? "ok" : "fail")}, got {outcome}");
                }
            }
        }

        Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
    }

    private static X509Certificate2 CreateCertificateAuthority()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Aspire Perl runtime trust tests", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        var now = DateTimeOffset.UtcNow;
        return request.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
    }

    private static X509Certificate2 CreateServerCertificate(X509Certificate2 authority)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=127.0.0.1", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        subjectAlternativeNames.AddIpAddress(IPAddress.Loopback);
        subjectAlternativeNames.AddDnsName("localhost");
        request.CertificateExtensions.Add(subjectAlternativeNames.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(authority, true, false));

        var serialNumber = RandomNumberGenerator.GetBytes(16);
        serialNumber[0] &= 0x7F;
        var notBefore = new DateTimeOffset(authority.NotBefore.ToUniversalTime()).AddMinutes(1);
        var notAfter = new DateTimeOffset(authority.NotAfter.ToUniversalTime()).AddMinutes(-1);
        using var certificate = request.Create(authority, notBefore, notAfter, serialNumber);
        using var certificateWithKey = certificate.CopyWithPrivateKey(key);

        // Round-trip through PKCS#12 so the private key is usable by the platform TLS stack.
        return X509CertificateLoader.LoadPkcs12(certificateWithKey.Export(X509ContentType.Pfx), null);
    }

    /// <summary>
    /// Minimal HTTPS responder. Clients that reject the certificate abort the handshake, which is expected.
    /// </summary>
    private sealed class TlsTestServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly X509Certificate2 _certificate;
        private readonly CancellationTokenSource _shutdown = new();
        private readonly Task _acceptLoop;

        private TlsTestServer(X509Certificate2 certificate)
        {
            _certificate = certificate;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            _acceptLoop = AcceptLoopAsync();
        }

        public string Url => $"https://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/";

        public static TlsTestServer Start(X509Certificate2 certificate) => new(certificate);

        public async ValueTask DisposeAsync()
        {
            await _shutdown.CancelAsync();
            _listener.Stop();
            await _acceptLoop;
            _shutdown.Dispose();
            _certificate.Dispose();
        }

        private async Task AcceptLoopAsync()
        {
            while (!_shutdown.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_shutdown.Token);
                }
                catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
                {
                    return;
                }

                _ = RespondAsync(client);
            }
        }

        private async Task RespondAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    await using var tls = new SslStream(client.GetStream());
                    await tls.AuthenticateAsServerAsync(
                        new SslServerAuthenticationOptions { ServerCertificate = _certificate },
                        _shutdown.Token);
                    await ReadRequestHeadersAsync(tls, _shutdown.Token);
                    await tls.WriteAsync(
                        "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok"u8.ToArray(),
                        _shutdown.Token);
                }
                catch (Exception ex) when (ex is AuthenticationException or IOException or OperationCanceledException or ObjectDisposedException)
                {
                }
            }
        }

        private static async Task ReadRequestHeadersAsync(Stream stream, CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            var received = new List<byte>();
            while (received.Count < 64 * 1024)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    return;
                }

                received.AddRange(buffer.AsSpan(0, read));
                if (received.Count >= 4 && received.TakeLast(4).SequenceEqual("\r\n\r\n"u8.ToArray()))
                {
                    return;
                }
            }
        }
    }

    // Writes one "<client> <target> ok|fail <detail>" line per probe. Modules are loaded lazily so
    // a missing client module is reported as a failed probe instead of aborting the script.
    private const string ProbeScript = """
        use strict;
        use warnings;

        my ($results_path, @targets) = @ARGV;
        open my $results, '>', $results_path or die "Cannot write $results_path: $!";

        my %probes = (
            'http-tiny' => sub {
                require HTTP::Tiny;
                my $response = HTTP::Tiny->new(verify_SSL => 1, timeout => 30)->get(shift);
                return ($response->{success}, "$response->{status} $response->{reason} $response->{content}");
            },
            'lwp' => sub {
                require LWP::UserAgent;
                require LWP::Protocol::https;
                my $response = LWP::UserAgent->new(timeout => 30)->get(shift);
                return ($response->is_success, $response->status_line);
            },
            'mojo' => sub {
                require Mojo::UserAgent;
                my $tx = Mojo::UserAgent->new(request_timeout => 30)->get(shift);
                my $error = $tx->error;
                return $error ? (0, $error->{message}) : (1, $tx->res->code);
            },
        );

        for my $target (@targets) {
            my ($name, $url) = split /=/, $target, 2;
            for my $client (sort keys %probes) {
                my ($ok, $detail) = eval { $probes{$client}->($url) };
                ($ok, $detail) = (0, $@) unless defined $ok;
                $detail = substr(($detail // '') =~ s/\s+/ /gr, 0, 300);
                print {$results} join(' ', $client, $name, $ok ? 'ok' : 'fail', $detail), "\n";
            }
        }

        close $results or die "Cannot close $results_path: $!";
        """;
}
