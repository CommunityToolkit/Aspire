using CommunityToolkit.Aspire.Hosting.ActiveMQ;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Base class form ActiveMQ Classic and Artemis server resources.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="userName">A parameter that contains the ActiveMQ server username, or <see langword="null"/> to use a default value.</param>
/// <param name="password">A parameter that contains the ActiveMQ server password.</param>
/// <param name="scheme">Scheme used in the connectionString (e.g. tcp or activemq, see MassTransit)</param>
/// <param name="settings">Settings being used for ActiveMQ Classic or Artemis</param>
public abstract class ActiveMQServerResourceBase(string name, ParameterResource? userName, ParameterResource password, string scheme, IActiveMQSettings settings) : ContainerResource(name), IResourceWithConnectionString, IResourceWithEnvironment
{
    internal const string PrimaryEndpointName = "tcp";
    internal const string DefaultUserName = "admin";
    internal EndpointReference? _primaryEndpoint;


    /// <inheritdoc />
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create(
            $"{scheme}://{UserNameReference}:{PasswordParameter}@{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}");

    /// <summary>
    /// Gets the primary endpoint for the ActiveMQ server.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new EndpointReference(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the parameter that contains the ActiveMQ server username.
    /// </summary>
    public ParameterResource? UserNameParameter { get; } = userName;

    /// <summary>
    /// Gets the parameter that contains the ActiveMQ server password.
    /// </summary>
    public ParameterResource PasswordParameter { get; } = ThrowIfNull(password);


    /// <summary>
    /// Gets the ActiveMQ settings.
    /// </summary>
    public IActiveMQSettings ActiveMqSettings { get; } = settings;

    internal ReferenceExpression UserNameReference =>
        UserNameParameter is not null ?
            ReferenceExpression.Create($"{UserNameParameter}") :
            ReferenceExpression.Create($"{DefaultUserName}");
    
    private static T ThrowIfNull<T>([NotNull] T? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        => argument ?? throw new ArgumentNullException(paramName);
}