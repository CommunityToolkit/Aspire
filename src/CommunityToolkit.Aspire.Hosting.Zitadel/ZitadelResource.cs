// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using System.Security.Cryptography.Xml;

namespace CommunityToolkit.Aspire.Hosting.Zitadel;

/// <summary>
/// A resource that represents a Zitadel resource.
/// <param name="name">The name of the resource.</param>
/// <param name="admin">A parameter that contains the Zitadel admin, or <see langword="null"/> to use a default value.</param>
/// <param name="adminPassword">A parameter that contains the Zitadel admin password.</param>
/// </summary>
public sealed class ZitadelResource(string name, ParameterResource? admin, ParameterResource adminPassword)
    : ContainerResource(name), IResourceWithServiceDiscovery
{
    private const string DefaultAdmin = "zitadel-admin";

    /// <summary>
    /// Gets the parameter that contains the Zitadel admin username.
    /// </summary>
    public ParameterResource? AdminUserNameParameter { get; } = admin;

    internal ReferenceExpression AdminReference
        => AdminUserNameParameter is not null
            ? ReferenceExpression.Create($"{AdminUserNameParameter}")
            : ReferenceExpression.Create($"{DefaultAdmin}");
    
    /// <summary>
    /// Gets the parameter that contains the Zitadel admin password.
    /// </summary>
    public ParameterResource AdminPasswordParameter { get; } = adminPassword ?? throw new ArgumentNullException(nameof(adminPassword));
}