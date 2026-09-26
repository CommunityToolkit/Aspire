// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Kind;

internal sealed class KindPostApplyChecks(IEnumerable<IKindPostApplyCheck> checks)
{
    public async Task RunAsync(KindDeployedResource resource, CancellationToken cancellationToken)
    {
        foreach (var check in checks)
        {
            await check.CheckAsync(resource, cancellationToken).ConfigureAwait(false);
        }
    }
}
