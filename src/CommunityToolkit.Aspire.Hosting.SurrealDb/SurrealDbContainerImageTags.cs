// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting;

internal sealed class SurrealDbContainerImageTags
{
    /// <summary>docker.io</summary>
    public const string Registry = "docker.io";
    /// <summary>surrealdb/surrealdb</summary>
    public const string Image = "surrealdb/surrealdb";
    /// <summary>v2.1</summary>
    public const string Tag = "v2.1";
    
    /// <summary>docker.io</summary>
    public const string SurrealistRegistry = "docker.io";
    /// <summary>surrealdb/surrealist</summary>
    public const string SurrealistImage = "surrealdb/surrealist";
    /// <summary>3.1.6</summary>
    public const string SurrealistTag = "3.1.6";
}