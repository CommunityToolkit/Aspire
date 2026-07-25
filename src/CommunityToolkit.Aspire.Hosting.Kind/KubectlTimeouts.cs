// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CommunityToolkit.Aspire.Hosting.Kind;

internal static class KubectlTimeouts
{
    internal static readonly TimeSpan MaximumTimeout = TimeSpan.FromHours(1);

    internal static TimeSpan Normalize(TimeSpan timeout, string parameterName)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName, timeout, "Timeout must be greater than zero.");
        }

        if (timeout > MaximumTimeout)
        {
            throw new ArgumentOutOfRangeException(parameterName, timeout, $"Timeout must be less than or equal to {MaximumTimeout}.");
        }

        return TimeSpan.FromSeconds(Math.Ceiling(timeout.TotalSeconds));
    }

    internal static int ToSeconds(TimeSpan timeout, string parameterName)
    {
        return (int)Normalize(timeout, parameterName).TotalSeconds;
    }
}
