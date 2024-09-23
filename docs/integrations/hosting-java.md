# CommunityToolkit.Aspire.Hosting.Java

[![CommunityToolkit.Aspire.Hosting.Azure.Java](https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Azure.Java)](https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Azure.Java/) | [![CommunityToolkit.Aspire.Hosting.Azure.Java (latest)](https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.Azure.Java?label=nuget%20(preview))](https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Azure.Java/absoluteLatest)

## Overview

This is a .NET Aspire Integration for Java/Spring applications.

It provides support for both container options and executable options for the Java/Spring app integration defined in the AppHost project.

## Prerequisites

- This integration requires the [OpenTelemetry Agent for Java](https://opentelemetry.io/docs/zero-code/java/agent/) to be downloaded and placed in the `agents` directory in the root of the project.

    ```bash
    # bash/zsh
    mkdir -p ./agents
    wget -P ./agents \
        https://github.com/open-telemetry/opentelemetry-java-instrumentation/releases/latest/download/opentelemetry-javaagent.jar
    
    # PowerShell
    New-item -type Directory -Path ./agents -Force
    Invoke-WebRequest `
        -OutFile "./agents/opentelemetry-javaagent.jar" `
        -Uri "https://github.com/open-telemetry/opentelemetry-java-instrumentation/releases/latest/download/opentelemetry-javaagent.jar"
    ```

## Usage

!!! note
    This integration requires the your Java/Spring application to be compiled and build through Maven, Gradle or any other build tool that generates a JAR file.

### Use the containerized Spring app

There are high chances that your Spring apps have already been containerized. In this case, you can use the `JavaAppContainerResourceOptions` to define the containerized Spring app.

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Define the containerized Spring app
var containerapp = builder.AddSpringApp("containerapp",
                           new JavaAppContainerResourceOptions()
                           {
                               ContainerImageName = "aliencube/aspire-spring-maven-sample",
                               OtelAgentPath = "/agents"
                           });

// Add reference to the web app
var webapp = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Java_WebApp>("webapp")
                    .WithExternalHttpEndpoints()
                    .WithReference(containerapp);

builder.Build().Run();
```

### Use the executable Spring app

If you want to directly use the JAR file to run the Spring app, you can use the `JavaAppExecutableResourceOptions` to define the executable Spring app. Make sure to add the `PublishAsDockerFile` option to publish the app using `Dockerfile` for the executable Spring app.

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Define the executable Spring app directly from the JAR file
var executableapp = builder.AddSpringApp("executableapp",
                           workingDirectory: "../CommunityToolkit.Aspire.Hosting.Java.Spring.Maven",
                           new JavaAppExecutableResourceOptions()
                           {
                               ApplicationName = "target/spring-maven-0.0.1-SNAPSHOT.jar",
                               Port = 8085,
                               OtelAgentPath = "../../../agents",
                           })
                           .PublishAsDockerFile(
                           [
                               new DockerBuildArg("JAR_NAME", "spring-maven-0.0.1-SNAPSHOT.jar"),
                               new DockerBuildArg("AGENT_PATH", "/agents"),
                               new DockerBuildArg("SERVER_PORT", "8085"),
                           ]);

// Add reference to the web app
var webapp = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Java_WebApp>("webapp")
                    .WithExternalHttpEndpoints()
                    .WithReference(executableapp);

builder.Build().Run();
```

The `Dockerfile` might vary depending on your Spring app, but at least those three build arguments should be defined: `JAR_NAME`, `AGENT_PATH`, and `SERVER_PORT`.

```dockerfile
...

# Set the default .jar file name
ARG JAR_NAME=spring-maven-0.0.1-SNAPSHOT.jar

# Copy the .jar file into the container at /app
COPY ./target/${JAR_NAME} app.jar

...

# Set the default path for the OpenTelemetry Java agent
ARG AGENT_PATH=/agents

# Download the OpenTelemetry Java agent
RUN mkdir -p ${AGENT_PATH}
RUN wget -P ${AGENT_PATH} https://github.com/open-telemetry/opentelemetry-java-instrumentation/releases/latest/download/opentelemetry-javaagent.jar
RUN chmod +x ${AGENT_PATH}/opentelemetry-javaagent.jar

# Set the default server port
ARG SERVER_PORT=8080
ENV SERVER_PORT=${SERVER_PORT}

# Make port available to the outside this container
EXPOSE ${SERVER_PORT}

...
```
