using Aspire.Hosting;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal interface IVercelCliRunner
{
    Task<VercelCliResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string? workingDirectory, CancellationToken cancellationToken, string? standardInput = null);
}

internal sealed class VercelCliRunner : IVercelCliRunner
{
    public async Task<VercelCliResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string? workingDirectory, CancellationToken cancellationToken, string? standardInput = null)
    {
        // Keep all target CLI calls behind this runner so unit tests can assert exact
        // executable/argument/stdin boundaries. Secrets use stdin; arguments are never
        // shell-concatenated, which avoids platform quoting differences.
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        // Vercel CLI can emit ANSI color codes that make parser failure messages harder to
        // read and snapshot. Disable them at the process boundary for deterministic output.
        startInfo.Environment["NO_COLOR"] = "1";

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new DistributedApplicationException($"The Vercel CLI process '{fileName}' could not be started.");

            var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            if (standardInput is not null)
            {
                await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken).ConfigureAwait(false);
                process.StandardInput.Close();
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var standardOutput = await standardOutputTask.ConfigureAwait(false);
            var standardError = await standardErrorTask.ConfigureAwait(false);

            return new VercelCliResult(process.ExitCode, standardOutput, standardError);
        }
        catch (Win32Exception ex)
        {
            throw new DistributedApplicationException(
                $"The Vercel CLI '{fileName}' could not be started. Install Vercel CLI from https://vercel.com/docs/cli and ensure it is available on PATH.",
                ex);
        }
    }
}

internal sealed record VercelCliResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}
