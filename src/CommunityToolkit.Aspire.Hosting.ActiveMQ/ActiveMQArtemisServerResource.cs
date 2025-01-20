// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Aspire.Hosting.ActiveMQ;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a ActiveMQ Artemis resource.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="userName">A parameter that contains the ActiveMQ server username, or <see langword="null"/> to use a default value.</param>
/// <param name="password">A parameter that contains the ActiveMQ server password.</param>
/// <param name="scheme">Scheme used in the connectionString (e.g. tcp or activemq, see MassTransit)</param>
public class ActiveMQArtemisServerResource(string name, ParameterResource? userName, ParameterResource password, string scheme) : ActiveMQServerResourceBase(name, userName, password, scheme, ActiveMQSettings.ForArtemis)
{
}
