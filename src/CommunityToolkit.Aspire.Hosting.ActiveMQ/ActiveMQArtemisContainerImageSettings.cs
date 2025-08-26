// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CommunityToolkit.Aspire.Hosting.ActiveMQ;

internal static class ActiveMQArtemisContainerImageSettings
{
    public const string Registry = "docker.io";
    public const string Image = "apache/activemq-artemis";
    public const string Tag = "2.42.0";
    public const string EnvironmentVariableUsername = "ARTEMIS_USER";
    public const string EnvironmentVariablePassword = "ARTEMIS_PASSWORD";
    public const string JolokiaPath = "/console/jolokia/read/org.apache.activemq.artemis:broker=%220.0.0.0%22/Started";
    public const string DataPath = "/var/lib/artemis-instance";
    public const string ConfPath = "/var/lib/artemis-instance/etc-override";

}
