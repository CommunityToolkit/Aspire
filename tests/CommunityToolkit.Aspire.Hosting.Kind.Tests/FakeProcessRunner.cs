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
    public List<ExecutedCommand> Commands { get; } = [];

    public ProcessResult NextResult { get; set; } = new(0, "", "");

    public Task<ProcessResult> RunAsync(
        ILogger logger,
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        Commands.Add(new(fileName, string.Join(" ", arguments), workingDirectory));
        return Task.FromResult(NextResult);
    }

    internal sealed record ExecutedCommand(string FileName, string Arguments, string? WorkingDirectory);
}
