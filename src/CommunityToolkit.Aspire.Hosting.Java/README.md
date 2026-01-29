# CommunityToolkit.Aspire.Hosting.Java library

Provides extensions methods and resource definitions for the Aspire AppHost to support running Java/Spring applications either using either the JDK or a container and configuring the OpenTelemetry agent for Java.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Java
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define a Java resource, then call `AddJavaApp` or `AddSpringApp`:

```csharp
// Define the Java container app resource
var containerapp = builder.AddSpringApp("containerapp",
                           new JavaAppContainerResourceOptions()
                           {
                               ContainerImageName = "<repository>/<image>",
                               OtelAgentPath = "<agent-path>"
                           });

// Define the Java executable app resource
var executableapp = builder.AddSpringApp("executableapp",
                           new JavaAppExecutableResourceOptions()
                           {
                               ApplicationName = "target/app.jar",
                               OtelAgentPath = "../../../agents"
                           });
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-java

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

