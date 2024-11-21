// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a ActiveMQ resource.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="userName">A parameter that contains the ActiveMQ server username, or <see langword="null"/> to use a default value.</param>
/// <param name="password">A parameter that contains the ActiveMQ server password.</param>
/// <param name="scheme">Scheme used in the connectionString (e.g. tcp or activemq, see MassTransit)</param>
public class ActiveMQServerResource(string name, ParameterResource? userName, ParameterResource password,
    string scheme) : ContainerResource(name), IResourceWithConnectionString, IResourceWithEnvironment
{
    private readonly string _scheme = scheme;
    internal const string PrimaryEndpointName = "tcp";
    private const string DefaultUserName = "admin";
    private EndpointReference? _primaryEndpoint;
    
    /// <summary>
    /// Gets the primary endpoint for the ActiveMQ server.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new EndpointReference(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the parameter that contains the ActiveMQ server username.
    /// </summary>
    public ParameterResource? UserNameParameter { get; } = userName;

    internal ReferenceExpression UserNameReference =>
        UserNameParameter is not null ?
            ReferenceExpression.Create($"{UserNameParameter}") :
            ReferenceExpression.Create($"{DefaultUserName}");

    /// <summary>
    /// Gets the parameter that contains the ActiveMQ server password.
    /// </summary>
    public ParameterResource PasswordParameter { get; } = ThrowIfNull(password);

    /// <summary>
    /// Gets the connection string expression for the ActiveMQ server.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create(
            $"{_scheme}://{UserNameReference}:{PasswordParameter}@{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}");
    
    private static T ThrowIfNull<T>([NotNull] T? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        => argument ?? throw new ArgumentNullException(paramName);
}
