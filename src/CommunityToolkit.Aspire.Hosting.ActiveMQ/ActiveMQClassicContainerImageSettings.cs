// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CommunityToolkit.Aspire.Hosting.ActiveMQ;

internal static class ActiveMQClassicContainerImageSettings
{
    public const string Registry = "docker.io";
    public const string Image = "apache/activemq";
    public const string Tag = "6.3.2";
    public const string EnvironmentVariableUsername = "ACTIVEMQ_CONNECTION_USER";
    public const string EnvironmentVariablePassword = "ACTIVEMQ_CONNECTION_PASSWORD";
    public const string DataPath = "/opt/apache-activemq/data";
    public const string ConfPath = "/opt/apache-activemq/conf";
}
