using System.Diagnostics;
using System.Text;
using Xunit.Sdk;

namespace CommunityToolkit.Aspire.Testing;

public static class ProcessTestUtilities
{
    public static async Task RunProcessAsync(string fileName, IEnumerable<string> arguments, string workingDirectory, CancellationToken cancellationToken = default)
    {
        ProcessStartInfo startInfo = new(fileName)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory
        };

        foreach (string argument in arguments) {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new()
        {
            StartInfo = startInfo
        };

        StringBuilder standardOutput = new();
        StringBuilder standardError = new();
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                standardOutput.AppendLine(args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                standardError.AppendLine(args.Data);
            }
        };

        if (!process.Start())
        {
            throw new XunitException($"Failed to start process '{fileName}'.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            string joinedArguments = string.Join(" ", arguments.Select(QuoteArgument));
            throw new XunitException(
                $"Command '{fileName} {joinedArguments}' failed with exit code {process.ExitCode}.{Environment.NewLine}" +
                $"Standard output:{Environment.NewLine}{standardOutput}{Environment.NewLine}" +
                $"Standard error:{Environment.NewLine}{standardError}");
        }
    }

    private static string QuoteArgument(string argument) =>
        argument.Contains(' ', StringComparison.Ordinal) ? $"\"{argument}\"" : argument;
}
