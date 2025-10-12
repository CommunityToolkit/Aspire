# CommunityToolkit.Aspire.Hosting.Java library

Provides extensions methods and resource definitions for the .NET Aspire AppHost to support running Java/Spring applications either using either the JDK or a container and configuring the OpenTelemetry agent for Java.

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

// Define a Java executable app with JVM arguments
var appWithJvmArgs = builder.AddJavaApp("app", "./app",
                           new JavaAppExecutableResourceOptions()
                           {
                               ApplicationName = "app.jar",
                               JvmArgs = ["-Xmx512m", "-Xms256m", "-Dconfig.path=/etc/config"],
                               Args = ["--spring.profiles.active=prod"]
                           });
```

### Configuration Options

The `JavaAppExecutableResourceOptions` class provides several configuration options:

- `ApplicationName`: The name of the JAR file to execute (default: "target/app.jar")
- `Port`: The port number for the application (default: 8080)
- `OtelAgentPath`: Path to the OpenTelemetry Java Agent
- `JvmArgs`: Array of JVM arguments to pass to the Java Virtual Machine (e.g., `-Xmx512m`, `-Dconfig.path=/etc/config`)
- `Args`: Array of application arguments to pass to the Java application

JVM arguments are passed before the `-jar` flag, while application arguments are passed after the JAR file name:
```
java [JvmArgs...] -jar app.jar [Args...]
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-java

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

