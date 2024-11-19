// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a ActiveMQ resource.
/// </summary>
public class ActiveMQServerResource : ContainerResource, IResourceWithConnectionString, IResourceWithEnvironment
{
    private readonly string _scheme;
    internal const string PrimaryEndpointName = "tcp";
    private const string DefaultUserName = "admin";

    /// <summary>
    /// Initializes a new instance of the <see cref="ActiveMQServerResource"/> class.
    /// </summary>
    /// <param name="name">The name of the resource.</param>
    /// <param name="userName">A parameter that contains the ActiveMQ server user name, or <see langword="null"/> to use a default value.</param>
    /// <param name="password">A parameter that contains the ActiveMQ server password.</param>
    /// <param name="scheme">Scheme used in the connectionstring (e.g. tcp or activemq, see MassTransit)</param>
    public ActiveMQServerResource(string name, ParameterResource? userName, ParameterResource password,
        string scheme) : base(name)
    {
        _scheme = scheme;
        ArgumentNullException.ThrowIfNull(password);

        PrimaryEndpoint = new EndpointReference(this, PrimaryEndpointName);
        UserNameParameter = userName;
        PasswordParameter = password;
    }

    /// <summary>
    /// Gets the primary endpoint for the ActiveMQ server.
    /// </summary>
    public EndpointReference PrimaryEndpoint { get; }

    /// <summary>
    /// Gets the parameter that contains the ActiveMQ server user name.
    /// </summary>
    public ParameterResource? UserNameParameter { get; }

    internal ReferenceExpression UserNameReference =>
        UserNameParameter is not null ?
            ReferenceExpression.Create($"{UserNameParameter}") :
            ReferenceExpression.Create($"{DefaultUserName}");

    /// <summary>
    /// Gets the parameter that contains the ActiveMQ server password.
    /// </summary>
    public ParameterResource PasswordParameter { get; }

    /// <summary>
    /// Gets the connection string expression for the ActiveMQ server.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create(
            $"{_scheme}://{UserNameReference}:{PasswordParameter}@{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}");
}
