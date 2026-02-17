# CommunityToolkit.Aspire.Hosting.Java library

Provides extension methods and resource definitions for the Aspire AppHost to support running Java applications.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Java
```

### Example usage

In the _Program.cs_ file of `AppHost`, you can add a Java application:

```csharp
var javaApp = builder.AddJavaApp("javaApp", "../java-project")
    .WithMavenBuild()
    .WithHttpEndpoint(env: "SERVER_PORT");
```

### Maven and Gradle support

The integration provides support for Maven and Gradle build systems.

To run a Maven build before the application starts:

```csharp
var javaApp = builder.AddJavaApp("javaApp", "../java-project")
    .WithMavenBuild();
```

To run a Gradle build before the application starts:

```csharp
var javaApp = builder.AddJavaApp("javaApp", "../java-project")
    .WithGradleBuild();
```

### Customizing the JVM arguments

You can customize the JVM arguments for the Java application:

```csharp
var javaApp = builder.AddJavaApp("javaApp", "../java-project")
    .WithJvmArgs("-Xmx512m", "-Xms256m");
```

### Configuring OpenTelemetry Agent

You can configure the OpenTelemetry Java Agent:

```csharp
var javaApp = builder.AddJavaApp("javaApp", "../java-project")
    .WithOtelAgent("../agents/opentelemetry-javaagent.jar");
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-java

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

