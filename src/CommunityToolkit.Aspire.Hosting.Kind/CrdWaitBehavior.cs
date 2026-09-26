// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREATS001 // AspireExport APIs are experimental

[assembly: Aspire.Hosting.AspireExport(typeof(Aspire.Hosting.CrdWaitBehavior))]

namespace Aspire.Hosting;

/// <summary>
/// Specifies how Kind manifest and Helm chart resources handle CRD Established-condition wait failures.
/// </summary>
public enum CrdWaitBehavior
{
    /// <summary>
    /// Fail the deployed resource when waiting for discovered CRDs fails or times out.
    /// </summary>
    Fail,

    /// <summary>
    /// Log a warning and continue with unverified CRD readiness when waiting fails or times out.
    /// </summary>
    BestEffort,
}

#pragma warning restore ASPIREATS001
