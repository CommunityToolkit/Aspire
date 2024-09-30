# Aspire.CommunityToolkit.Azure.Hosting.DataApiBuilder

This enables a containerized Data Api Builder to be integrated with .NET Aspire.

## Prerequisites

- [Docker](https://docs.docker.com/get-docker/)

## Quickstart

1. Run .NET Aspire dashboard:

    ```bash
    dotnet watch run --project ./examples/DataApiBuilder/CommunityToolkit.Aspire.DataApiBuilder.AppHost
    ```

## More detailed steps (Optional)

1. ??


## Dashboard

1. Check the dashboard that both containerized API and executable app are up and running.

 

## Deployment to Azure

1. Change the directory to `examples/DataApiBuilder`.

    ```bash
    cd ./examples/DataApiBuilder
    ```

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
