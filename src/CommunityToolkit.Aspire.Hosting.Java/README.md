# CommunityToolkit.Aspire.Hosting.Java

Provides extension methods and resource definitions for the .NET Aspire AppHost to support running Java applications. The integration supports Maven, Gradle, and standalone JAR-based applications.

## Install the package

In your AppHost project, install the package:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Java
```

## Add a Java application resource

### Run with Maven

Use `WithMavenGoal` to run the application using a Maven goal:

```csharp
var app = builder.AddJavaApp("app", "../java-project")
    .WithMavenGoal("spring-boot:run")
    .WithHttpEndpoint(targetPort: 8080, env: "SERVER_PORT");
```

Pass additional arguments:

```csharp
var app = builder.AddJavaApp("app", "../java-project")
    .WithMavenGoal("spring-boot:run", "-Dspring-boot.run.profiles=dev")
    .WithHttpEndpoint(targetPort: 8080, env: "SERVER_PORT");
```

### Run with Gradle

Use `WithGradleTask` to run the application using a Gradle task:

```csharp
var app = builder.AddJavaApp("app", "../java-project")
    .WithGradleTask("bootRun")
    .WithHttpEndpoint(targetPort: 8080, env: "SERVER_PORT");
```

### Run with a JAR file

To run an existing JAR file, pass the `jarPath` parameter:

```csharp
var app = builder.AddJavaApp("app", "../java-project", "target/app.jar")
    .WithHttpEndpoint(targetPort: 8080, env: "SERVER_PORT");
```

## Build before run

Use `WithMavenBuild` or `WithGradleBuild` to compile the application before it starts. This is typically not needed when using `WithMavenGoal` or `WithGradleTask`, as those goals usually handle building automatically.

```csharp
var app = builder.AddJavaApp("app", "../java-project", "target/app.jar")
    .WithMavenBuild()
    .WithHttpEndpoint(targetPort: 8080, env: "SERVER_PORT");
```

## Publish as a container

When you run `aspire publish`, executable Java apps are published as a multi-stage Dockerfile-based container image.

- `AddJavaApp(..., jarPath)` copies the configured JAR into the runtime image. If that JAR is produced during publish, pair it with `WithMavenBuild` or `WithGradleBuild`.
- Apps configured with `WithMavenGoal` or `WithGradleTask` keep using those commands in run mode. During publish, the integration switches to a packaging command in the container build so the produced JAR can be copied into the runtime image.
- By default, the generated Dockerfile uses `eclipse-temurin:21-jdk` for the build stage and `eclipse-temurin:21-jre` for the runtime stage.
- If the application directory already contains a `Dockerfile`, publish uses that file instead of generating one.
- If you customize the wrapper path, keep the wrapper script inside the application's working directory so it is available in the Docker build context.

## Add a containerized Java application

To run a Java application from a container image, use `AddJavaContainerApp`:

```csharp
var app = builder.AddJavaContainerApp("app", "my-java-image", "latest")
    .WithHttpEndpoint(targetPort: 8080, env: "SERVER_PORT");
```

## JVM configuration

### JVM arguments

Use `WithJvmArgs` to configure JVM arguments:

```csharp
var app = builder.AddJavaApp("app", "../java-project")
    .WithMavenGoal("spring-boot:run")
    .WithJvmArgs(["-Xmx512m", "-Xms256m"])
    .WithHttpEndpoint(targetPort: 8080, env: "SERVER_PORT");
```

### OpenTelemetry agent

Use `WithOtelAgent` to configure the OpenTelemetry Java Agent:

```csharp
var app = builder.AddJavaApp("app", "../java-project", "target/app.jar")
    .WithOtelAgent("../agents/opentelemetry-javaagent.jar")
    .WithHttpEndpoint(targetPort: 8080, env: "SERVER_PORT");
```

## Additional information

- [Aspire Community Toolkit: Java hosting](https://aspire.dev/integrations/frameworks/java/)

## Feedback and contributing

- [GitHub repository](https://github.com/CommunityToolkit/Aspire)
