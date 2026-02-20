# CommunityToolkit.Aspire.Hosting.Java library

Provides extension methods and resource definitions for the Aspire AppHost to support running Java applications.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Java
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define a Java resource, then call `AddJavaApp`:

```csharp
var javaApp = builder.AddJavaApp("javaApp", "../java-project", "target/app.jar")
    .WithMavenBuild()
    .WithHttpEndpoint(env: "SERVER_PORT");
```

The `SERVER_PORT` environment variable is used to determine the port the Java application should listen on. It is randomly assigned by Aspire. The name of the environment variable can be changed by passing a different value to the `WithHttpEndpoint` method.

The `jarPath` parameter specifies the path to the jar file, relative to the resource working directory.

### Containerized Java applications

To run a containerized Java application, use `AddJavaContainerApp`:

```csharp
var javaApp = builder.AddJavaContainerApp("javaApp", "my-java-image", "latest");
```

## Build Systems

The integration provides support for Maven and Gradle build systems. Build resources are created in run mode only — they do not run when publishing, as the generated Dockerfile handles the build automatically.

### Maven

To run a Maven build (`mvnw clean package`) before the application starts:

```csharp
var javaApp = builder.AddJavaApp("javaApp", "../java-project", "target/app.jar")
    .WithMavenBuild();
```

### Gradle

To run a Gradle build (`gradlew clean build`) before the application starts:

```csharp
var javaApp = builder.AddJavaApp("javaApp", "../java-project", "build/libs/app.jar")
    .WithGradleBuild();
```

### Custom build arguments

You can provide custom build arguments, replacing the defaults (`clean package` for Maven, `clean build` for Gradle):

```csharp
var javaApp = builder.AddJavaApp("javaApp", "../java-project", "target/app.jar")
    .WithMavenBuild(args: ["clean", "install", "-DskipTests"]);
```

### Custom wrapper script

You can override the wrapper script path, relative to the resource working directory:

```csharp
var javaApp = builder.AddJavaApp("javaApp", "../java-project", "build/libs/app.jar")
    .WithGradleBuild(wrapperScript: "scripts/gradlew");
```

## JVM Configuration

### JVM arguments

You can customize the JVM arguments for the Java application:

```csharp
var javaApp = builder.AddJavaApp("javaApp", "../java-project", "target/app.jar")
    .WithJvmArgs("-Xmx512m", "-Xms256m");
```

### OpenTelemetry Agent

You can configure the OpenTelemetry Java Agent:

```csharp
var javaApp = builder.AddJavaApp("javaApp", "../java-project", "target/app.jar")
    .WithOtelAgent("../agents/opentelemetry-javaagent.jar");
```

## Publishing

When publishing your Aspire application with a Maven or Gradle build configured, the Java resource automatically generates a multi-stage Dockerfile for containerization. To enable this, call `PublishAsJavaDockerfile`:

```csharp
var javaApp = builder.AddJavaApp("javaApp", "../java-project", "target/app.jar")
    .WithMavenBuild()
    .PublishAsJavaDockerfile();
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-java

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
