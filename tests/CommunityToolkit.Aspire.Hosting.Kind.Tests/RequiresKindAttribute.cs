// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using CommunityToolkit.Aspire.Testing;
using Xunit.v3;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

/// <summary>
/// Marks a test as requiring the Kind CLI to be installed.
/// Tests are skipped (marked as "failing" trait) when Kind is not available.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresKindAttribute : Attribute, ITraitAttribute
{
    private static readonly Lazy<bool> _isAvailable = new(CheckKindAvailable);

    public static bool IsSupported => RequiresDockerAttribute.IsSupported && _isAvailable.Value;

    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
    {
        if (!IsSupported)
        {
            return [new KeyValuePair<string, string>("category", "failing")];
        }

        return [];
    }

    private static bool CheckKindAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "kind",
                Arguments = "version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                return false;
            }

            if (!process.WaitForExit(TimeSpan.FromSeconds(5)))
            {
                // The kind process did not exit within the timeout. Kill it so we don't
                // leak a running process, and treat kind as unavailable.
                process.Kill(entireProcessTree: true);
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
