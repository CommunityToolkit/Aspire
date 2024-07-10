# Aspire Contribs

This is a collection of community contributed libraries for .NET Aspire

## Prerequisites

- [JDK 17+](https://learn.microsoft.com/java/openjdk/download)
- [Springboot CLI](https://docs.spring.io/spring-boot/installing.html#getting-started.installing.cli)
- [Apache Maven](https://maven.apache.org)
- [Docker](https://docs.docker.com/get-docker/)

## For Java App

1. First of all, you should have [OpenTelemetry Agent for Java](https://opentelemetry.io/docs/zero-code/java/agent/). You can download it to your local machine by running the following commands:

    ```bash
    # Bash
    mkdir -p ./agents
    wget -P ./agents \
        https://github.com/open-telemetry/opentelemetry-java-instrumentation/releases/latest/download/opentelemetry-javaagent.jar
    
    # PowerShell
    New-item -type Directory -Path ./downloaded -Force
    Invoke-WebRequest `
        -OutFile "./agents/opentelemetry-javaagent.jar" `
        -Uri "https://github.com/open-telemetry/opentelemetry-java-instrumentation/releases/latest/download/opentelemetry-javaagent.jar"
    ```

1. Build the Spring app with Maven:

    ```bash
    pushd ./src/Aspire.Contribs.Spring.Maven

    ./mvnw clean package

    popd
    ```

1. Build a container image for the Spring app:

    ```bash
    pushd ./src/Aspire.Contribs.Spring.Maven

    docker build . -t aspire-spring-maven-sample:latest

    popd
    ```

1. Push the container image to [Docker Hub](https://hub.docker.com) under your organisation. This sample uses `aliencube`:

    ```bash
    docker tag aspire-spring-maven-sample:latest aliencube/aspire-spring-maven-sample:latest
    docker push aliencube/aspire-spring-maven-sample:latest
    ```

   > **NOTE**: You need to log in to Docker Hub before pushing the image.

1. Run .NET Aspire dashboard:

    ```bash
    dotnet watch run --project ./src/Aspire.Contribs.AppHost
    ```

1. Check the dashboard that both containerised app and executable app are up and running.

    ![Aspire Dashboard](./images/dashboard.png)

1. Open the web app in your browser and navigate to the `/weather` page and see the weather information from ASP.NET Core Web API app, Spring container app and Spring executable app.

    ![Weather Page](./images/weather.png)

## For Python App

TBD

## For Azure API Management

TBD

## Deployment to Azure

1. Get the Azure environment name ready:

    ```bash
    # Bash
    AZURE_ENV_NAME="contribs$((RANDOM%9000+1000))"

    # PowerShell
    $AZURE_ENV_NAME="contribs$((Get-Random -Minimum 1000 -Maximum 9999))"
    ```

1. Run the following command to deploy the app to Azure:

    ```bash
    azd up -e $AZURE_ENV_NAME
    ```

   Follow the instruction for the rest of the deployment process.
