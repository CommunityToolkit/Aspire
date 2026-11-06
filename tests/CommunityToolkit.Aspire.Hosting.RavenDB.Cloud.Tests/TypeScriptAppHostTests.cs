using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Aspire.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.RavenDB.Cloud.Tests;

/// <summary>
/// The TypeScript example AppHost through the Aspire CLI, which must match the Aspire version of the repository.
/// </summary>
public class TypeScriptAppHostTests
{
    private static readonly string s_appHost = Path.GetFullPath(Path.Combine(
        "..", "..", "..", "..", "..", "examples", "ravendb", "CommunityToolkit.Aspire.Hosting.RavenDB.Cloud.AppHost.TypeScript"));

    [Fact]
    public async Task PublishReferencesTheDeployTimeValues()
    {
        var output = Directory.CreateTempSubdirectory(".ravendb-cloud-ts-publish");

        try
        {
            var result = await RunAspireAsync(["publish", "--apphost", "apphost.mts", "-o", output.FullName, "--non-interactive", "--nologo"], new Dictionary<string, string>());
            Assert.True(result.ExitCode == 0, result.Output);

            var compose = File.ReadAllText(Path.Combine(output.FullName, "docker-compose.yaml"));
            Assert.DoesNotContain("ravendb/ravendb", compose, StringComparison.Ordinal);
            Assert.Contains("ConnectionStrings__ravendb: \"URL=${RAVENDB_URL}\"", compose, StringComparison.Ordinal);
            Assert.Contains("Aspire__RavenDB__Client__ravendb__CertificateBase64: \"${RAVENDB_APISERVICE_CERTIFICATE}\"", compose, StringComparison.Ordinal);
        }
        finally
        {
            output.Delete(recursive: true);
        }
    }

    /// <summary>
    /// The spec and the options travel from TypeScript callbacks: a lost callback would fall back to defaults without a
    /// word, so the plan, computed against a local stand-in of the Cloud API, has to show exactly what the AppHost set.
    /// </summary>
    [Fact]
    public async Task PlanShowsWhatTheTypeScriptCallbacksSet()
    {
        var api = new FakeRavenDBCloudApi();
        await using var server = await ServeAsync(api);

        var result = await RunAspireAsync(
            ["do", "ravendb-cloud-plan-ravendb", "--apphost", "apphost.mts", "--non-interactive", "--nologo"],
            new Dictionary<string, string>
            {
                ["RAVENDB_CLOUD_ENDPOINT"] = server.Url,
                ["Parameters__ravendb-cloud-api-key"] = "test-api-key",
            });

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.True(
            // The CLI wraps long lines to the console width.
            Regex.Replace(result.Output, @"\s+", " ").Contains("create 'aspire-ravendb-example': Development Dev10, Aws eu-central-1, 10 GB SsdStandard on SingleDataDisk, channel Stable72, allowed IPs 203.0.113.0/24", StringComparison.Ordinal),
            result.Output);

        // A plan never changes anything.
        Assert.DoesNotContain(api.Requests, r => r.StartsWith("POST", StringComparison.Ordinal));
    }

    /// <summary>
    /// The same plan against the real Cloud API, so a change of the API or of what RavenDB Cloud offers shows up here
    /// rather than in a deployment. Needs a key in <c>RAVENDB_CLOUD_API_KEY</c>. The plan only reads.
    /// </summary>
    [Fact]
    [RequiresAuthenticatedTool("ravendb-cloud", "Calls the RavenDB Cloud API with a real key.")]
    public async Task PlanAgainstTheCloudApi()
    {
        var apiKey = Environment.GetEnvironmentVariable("RAVENDB_CLOUD_API_KEY");
        Assert.SkipWhen(string.IsNullOrEmpty(apiKey), "Set RAVENDB_CLOUD_API_KEY to plan against RavenDB Cloud.");

        var result = await RunAspireAsync(
            ["do", "ravendb-cloud-plan-ravendb", "--apphost", "apphost.mts", "--non-interactive", "--nologo"],
            new Dictionary<string, string> { ["Parameters__ravendb-cloud-api-key"] = apiKey! });

        // RavenDB Cloud offers the example's spec: the plan creates the product, or finds it as specified.
        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains("'aspire-ravendb-example'", Regex.Replace(result.Output, @"\s+", " "), StringComparison.Ordinal);
    }

    private static async Task<(int ExitCode, string Output)> RunAspireAsync(IEnumerable<string> arguments, IReadOnlyDictionary<string, string> environment)
    {
        var start = new ProcessStartInfo("aspire")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = s_appHost,
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        // Aspire saves parameter values in the deployment state of the AppHost, and a saved value wins over the
        // environment: without this, one run's API key would be used by every later run on the machine.
        start.Environment["Pipeline__ClearCache"] = "true";

        foreach (var (key, value) in environment)
        {
            start.Environment[key] = value;
        }

        using var process = new Process { StartInfo = start };
        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) { lock (output) { output.AppendLine(e.Data); } } };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { lock (output) { output.AppendLine(e.Data); } } };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        process.WaitForExit();

        return (process.ExitCode, output.ToString());
    }

    /// <summary>Serves <paramref name="api"/> over HTTP on a free loopback port.</summary>
    private static async Task<FakeApiServer> ServeAsync(FakeRavenDBCloudApi api)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var app = builder.Build();
        var invoker = new HttpMessageInvoker(api, disposeHandler: false);

        app.Run(async context =>
        {
            using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), $"http://fake{context.Request.Path}{context.Request.QueryString}");

            foreach (var header in context.Request.Headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }

            if (context.Request.ContentLength > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
            {
                using var body = new MemoryStream();
                await context.Request.Body.CopyToAsync(body);
                request.Content = new ByteArrayContent(body.ToArray());
            }

            using var response = await invoker.SendAsync(request, context.RequestAborted);
            context.Response.StatusCode = (int)response.StatusCode;
            await response.Content.CopyToAsync(context.Response.Body);
        });

        await app.StartAsync(TestContext.Current.CancellationToken);

        var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return new FakeApiServer(app, url);
    }

    private sealed class FakeApiServer(WebApplication app, string url) : IAsyncDisposable
    {
        public string Url { get; } = url;

        public ValueTask DisposeAsync() => app.DisposeAsync();
    }
}
