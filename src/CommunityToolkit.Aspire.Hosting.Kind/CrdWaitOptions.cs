// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREATS001 // AspireExport APIs are experimental

namespace Aspire.Hosting;

/// <summary>
/// Options for waiting for CustomResourceDefinitions created by a Kind deployment.
/// </summary>
[AspireExport(ExposeProperties = true)]
public sealed class CrdWaitOptions
{
    private TimeSpan _timeout = CommunityToolkit.Aspire.Hosting.Kind.KubectlTimeouts.DefaultCrdWaitTimeout;

    /// <summary>
    /// Gets or sets the maximum time to wait for discovered CRDs to reach the <c>Established</c> condition.
    /// </summary>
    /// <remarks>
    /// Defaults to five minutes. The configured value must be greater than zero and no more than one hour;
    /// fractional seconds are rounded up.
    /// </remarks>
    public TimeSpan Timeout
    {
        get => _timeout;
        set => _timeout = CommunityToolkit.Aspire.Hosting.Kind.KubectlTimeouts.Normalize(value, nameof(Timeout));
    }

    /// <summary>
    /// Gets or sets how CRD wait failures are handled.
    /// </summary>
    /// <remarks>Defaults to <see cref="CrdWaitBehavior.Fail"/>.</remarks>
    public CrdWaitBehavior FailureBehavior { get; set; } = CrdWaitBehavior.Fail;
}

#pragma warning restore ASPIREATS001
