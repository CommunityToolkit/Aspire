// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.ExceptionServices;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging.Abstractions;

namespace CommunityToolkit.Aspire.Hosting.Agents.Tests;

internal static class EnvironmentVariableEvaluator
{
    public static async ValueTask<Dictionary<string, string>> GetEnvironmentVariablesAsync(
        IResource resource,
        DistributedApplicationOperation operation,
        IServiceProvider services)
    {
        var context = new DistributedApplicationExecutionContext(
            new DistributedApplicationExecutionContextOptions(operation) { Services = services });
        var configuration = await ExecutionConfigurationBuilder.Create(resource)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(context, NullLogger.Instance, CancellationToken.None);
        if (configuration.Exception is not null)
        {
            ExceptionDispatchInfo.Throw(configuration.Exception);
        }
        return configuration.EnvironmentVariables.ToDictionary();
    }
}
