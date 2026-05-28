// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Kind;

/// <summary>
/// The result of a process execution.
/// </summary>
internal readonly record struct ProcessResult(int ExitCode, string Output, string Error);

/// <summary>
/// Abstraction for running external processes. Enables testing pipeline
/// steps and cluster management without shelling out to real CLI tools.
/// </summary>
internal interface IProcessRunner
{
    /// <summary>
    /// Runs a process asynchronously and returns its result.
    /// </summary>
    Task<ProcessResult> RunAsync(
        ILogger logger,
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environmentVariables = null,
        CancellationToken cancellationToken = default);
}
