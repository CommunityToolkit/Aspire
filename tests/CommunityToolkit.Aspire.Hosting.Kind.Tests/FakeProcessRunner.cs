// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Aspire.Hosting.Kind;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

/// <summary>
/// Records all commands executed via <see cref="IProcessRunner"/>
/// without shelling out to real CLI tools.
/// </summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    private readonly object _lock = new();

    public List<ExecutedCommand> Commands { get; } = [];

    public ProcessResult NextResult { get; set; } = new(0, "", "");
    public Queue<ProcessResult> Results { get; } = new();
    public Dictionary<string, ProcessResult> ResultsByFileName { get; } = [];

    public Task<ProcessResult> RunAsync(
        ILogger logger,
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environmentVariables = null,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            Commands.Add(new(fileName, string.Join(" ", arguments), workingDirectory, environmentVariables));

            return Task.FromResult(
                ResultsByFileName.TryGetValue(fileName, out var result)
                    ? result
                    : Results.Count > 0
                        ? Results.Dequeue()
                        : NextResult);
        }
    }

    internal sealed record ExecutedCommand(
        string FileName,
        string Arguments,
        string? WorkingDirectory,
        IReadOnlyDictionary<string, string>? EnvironmentVariables);
}
