// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Kind;

/// <summary>
/// The result of a process execution.
/// </summary>
internal readonly record struct ProcessResult(int ExitCode, string Output, string Error);

/// <summary>
/// Lightweight helper for running CLI tools (kind, docker) and capturing output.
/// </summary>
internal static class ProcessHelper
{
    /// <summary>
    /// Runs a process and returns its exit code, stdout, and stderr.
    /// </summary>
    internal static async Task<ProcessResult> RunAsync(
        ILogger logger,
        string fileName,
        string arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Executing: {FileName} {Arguments}", fileName, arguments);

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (workingDirectory is not null)
        {
            psi.WorkingDirectory = workingDirectory;
        }

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
                logger.LogDebug("[{FileName}] {Data}", fileName, e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
                logger.LogDebug("[{FileName}] stderr: {Data}", fileName, e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Process already exited.
            }

            throw;
        }

        return new ProcessResult(process.ExitCode, stdout.ToString().TrimEnd(), stderr.ToString().TrimEnd());
    }
}
