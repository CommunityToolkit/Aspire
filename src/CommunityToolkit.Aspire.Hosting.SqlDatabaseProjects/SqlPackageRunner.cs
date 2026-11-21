using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Xml.Linq;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects;

/// <summary>
/// Specifies a SqlPackage action used by the DACPAC pipeline.
/// </summary>
internal enum SqlPackageAction
{
    /// <summary>
    /// Generates a deployment report without modifying the target database.
    /// </summary>
    DeployReport,

    /// <summary>
    /// Generates a deployment script without executing it.
    /// </summary>
    Script,

    /// <summary>
    /// Publishes the DACPAC to the target database.
    /// </summary>
    Publish
}

/// <summary>
/// Describes one SqlPackage invocation.
/// </summary>
/// <param name="Action">The action to execute.</param>
/// <param name="SqlPackageVersion">The Microsoft.SqlPackage tool version.</param>
/// <param name="DacpacPath">The source DACPAC path.</param>
/// <param name="OutputPath">The optional report or script output path.</param>
/// <param name="ConnectionString">The resolved target connection string.</param>
/// <param name="TargetDatabaseName">The optional target database name override.</param>
/// <param name="ProfilePath">The optional user publish profile path.</param>
internal sealed record SqlPackageRunRequest(
    SqlPackageAction Action,
    string SqlPackageVersion,
    string DacpacPath,
    string? OutputPath,
    string ConnectionString,
    string? TargetDatabaseName,
    string? ProfilePath);

/// <summary>
/// Runs SqlPackage operations for the DACPAC pipeline.
/// </summary>
internal interface ISqlPackageRunner
{
    /// <summary>
    /// Runs the requested SqlPackage operation.
    /// </summary>
    /// <param name="request">The operation details.</param>
    /// <param name="logger">The pipeline logger.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task RunAsync(SqlPackageRunRequest request, ILogger logger, CancellationToken cancellationToken);
}

/// <summary>
/// Runs Microsoft.SqlPackage through the .NET tool host.
/// </summary>
/// <param name="hostEnvironment">The AppHost environment.</param>
internal sealed class SqlPackageRunner(IHostEnvironment hostEnvironment) : ISqlPackageRunner
{
    public async Task RunAsync(
        SqlPackageRunRequest request,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var profilePath = Path.Combine(temporaryDirectory, "deployment.publish.xml");
            var profileSecrets = CreateProfile(request, profilePath);
            await ProtectProfileAsync(profilePath, cancellationToken);
            var redactor = SqlPackageSecretRedactor.Create(
                [request.ConnectionString, .. profileSecrets]);
            var outputDirectory = Path.GetDirectoryName(request.OutputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
                WorkingDirectory = hostEnvironment.ContentRootPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in BuildArguments(request, profilePath))
            {
                processStartInfo.ArgumentList.Add(argument);
            }

            await RunProcessAsync(
                processStartInfo,
                redactor,
                request.Action,
                logger,
                cancellationToken);
        }
        finally
        {
            try
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(
                    ex,
                    "Unable to remove temporary SqlPackage profile directory {TemporaryDirectory}.",
                    temporaryDirectory);
            }
        }
    }

    internal static string[] CreateProfile(SqlPackageRunRequest request, string outputPath)
    {
        XDocument document;
        if (string.IsNullOrWhiteSpace(request.ProfilePath))
        {
            document = new XDocument(
                new XElement(
                    XName.Get("Project", "http://schemas.microsoft.com/developer/msbuild/2003"),
                    new XElement(
                        XName.Get("PropertyGroup", "http://schemas.microsoft.com/developer/msbuild/2003"))));
        }
        else
        {
            var profilePath = Path.GetFullPath(request.ProfilePath);
            if (!File.Exists(profilePath))
            {
                throw new DistributedApplicationException(
                    $"DACPAC publish profile '{profilePath}' was not found.");
            }

            document = XDocument.Load(profilePath);
        }

        var root = document.Root
            ?? throw new DistributedApplicationException("The DACPAC publish profile has no root element.");
        var properties = root.Elements()
            .FirstOrDefault(element => element.Name.LocalName == "PropertyGroup")
            ?? new XElement(XName.Get("PropertyGroup", root.Name.NamespaceName));
        if (properties.Parent is null)
        {
            root.Add(properties);
        }

        var secrets = document.Descendants()
            .Where(element => element.Name.LocalName == "TargetConnectionString")
            .Select(element => element.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        foreach (var target in document.Descendants().Where(element =>
                     element.Name.LocalName is "TargetConnectionString" or "TargetDatabaseName").ToArray())
        {
            target.Remove();
        }

        if (properties.Elements().All(element =>
                element.Name.LocalName != "BlockOnPossibleDataLoss"))
        {
            properties.Add(new XElement(
                XName.Get("BlockOnPossibleDataLoss", root.Name.NamespaceName),
                bool.TrueString));
        }

        properties.Add(new XElement(
            XName.Get("TargetConnectionString", root.Name.NamespaceName),
            request.ConnectionString));
        if (!string.IsNullOrWhiteSpace(request.TargetDatabaseName))
        {
            properties.Add(new XElement(
                XName.Get("TargetDatabaseName", root.Name.NamespaceName),
                request.TargetDatabaseName));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        document.Save(outputPath);
        return secrets;
    }

    internal static string[] BuildArguments(SqlPackageRunRequest request, string profilePath)
        =>
        [
            "dnx",
            $"Microsoft.SqlPackage@{request.SqlPackageVersion}",
            "--yes",
            "--",
            $"/Action:{request.Action}",
            $"/SourceFile:{request.DacpacPath}",
            $"/Profile:{profilePath}",
            .. GetOutputArguments(request)
        ];

    private static IEnumerable<string> GetOutputArguments(SqlPackageRunRequest request)
    {
        if (request.Action == SqlPackageAction.Publish)
        {
            yield break;
        }

        yield return request.Action == SqlPackageAction.DeployReport
            ? $"/OutputPath:{RequireOutputPath(request)}"
            : $"/DeployScriptPath:{RequireOutputPath(request)}";
    }

    private static void Validate(SqlPackageRunRequest request)
    {
        if (!File.Exists(request.DacpacPath))
        {
            throw new DistributedApplicationException(
                $"DACPAC '{request.DacpacPath}' was not found.");
        }

        if (string.IsNullOrWhiteSpace(request.ConnectionString))
        {
            throw new DistributedApplicationException(
                "The target connection string could not be resolved for the SqlPackage operation.");
        }

        if (request.Action != SqlPackageAction.Publish && string.IsNullOrWhiteSpace(request.OutputPath))
        {
            throw new DistributedApplicationException(
                $"SqlPackage action '{request.Action}' requires an output path.");
        }
    }

    private static string RequireOutputPath(SqlPackageRunRequest request)
        => request.OutputPath
            ?? throw new DistributedApplicationException(
                $"SqlPackage action '{request.Action}' requires an output path.");

    internal static async Task RunProcessAsync(
        ProcessStartInfo startInfo,
        SqlPackageSecretRedactor redactor,
        SqlPackageAction action,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new DistributedApplicationException(
                $"Unable to start SqlPackage for action '{action}'.");
        }

        var recentOutput = new ConcurrentQueue<string>();
        var standardOutputTask = CopyOutputAsync(
            process.StandardOutput,
            logger,
            redactor,
            recentOutput,
            cancellationToken);
        var standardErrorTask = CopyOutputAsync(
            process.StandardError,
            logger,
            redactor,
            recentOutput,
            cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            await Task.WhenAll(standardOutputTask, standardErrorTask);
            throw;
        }

        await Task.WhenAll(standardOutputTask, standardErrorTask);
        if (process.ExitCode != 0)
        {
            var diagnostics = string.Join(Environment.NewLine, recentOutput);
            throw new DistributedApplicationException(
                $"SqlPackage action '{action}' failed with exit code {process.ExitCode}.{Environment.NewLine}{diagnostics}");
        }
    }

    private static async Task CopyOutputAsync(
        StreamReader reader,
        ILogger logger,
        SqlPackageSecretRedactor redactor,
        ConcurrentQueue<string> recentOutput,
        CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            var redacted = redactor.Redact(line);
            logger.LogInformation("SqlPackage: {Output}", redacted);
            recentOutput.Enqueue(redacted);
            while (recentOutput.Count > 30)
            {
                recentOutput.TryDequeue(out _);
            }
        }
    }

    private static async Task ProtectProfileAsync(string path, CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows())
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var sid = identity.User?.Value
                ?? throw new DistributedApplicationException(
                    "Unable to determine the current Windows identity for the temporary SqlPackage profile.");
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "icacls",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            processStartInfo.ArgumentList.Add(path);
            processStartInfo.ArgumentList.Add("/inheritance:r");
            processStartInfo.ArgumentList.Add("/grant:r");
            processStartInfo.ArgumentList.Add($"*{sid}:(R)");

            using var process = new Process { StartInfo = processStartInfo };
            if (!process.Start())
            {
                throw new DistributedApplicationException(
                    "Unable to restrict access to the temporary SqlPackage profile.");
            }

            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = process.StandardError.ReadToEndAsync(cancellationToken);
            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                }

                await Task.WhenAll(output, error);
                throw;
            }

            if (process.ExitCode != 0)
            {
                var diagnostics = (await error).Trim();
                throw new DistributedApplicationException(
                    $"Unable to restrict access to the temporary SqlPackage profile.{Environment.NewLine}{diagnostics}");
            }

            _ = await output;
        }
        else
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}

/// <summary>
/// Redacts configured secret values and connection-string fragments from SqlPackage output.
/// </summary>
/// <param name="secrets">The exact values and fragments to redact.</param>
internal sealed class SqlPackageSecretRedactor(IReadOnlyList<string> secrets)
{
    public string Redact(string value)
    {
        foreach (var secret in secrets)
        {
            value = value.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
        }

        return value;
    }

    public static SqlPackageSecretRedactor Create(IEnumerable<string> values)
    {
        var secrets = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split(['\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .SelectMany(value => new[] { value, GetAssignmentValue(value) })
                .OfType<string>()
            .Where(value => value is { Length: >= 4 })
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(value => value.Length)
            .ToArray();

        return new(secrets);
    }

    private static string? GetAssignmentValue(string value)
    {
        var index = value.IndexOf('=', StringComparison.Ordinal);
        return index >= 0 && index < value.Length - 1
            ? value[(index + 1)..].Trim()
            : null;
    }
}
