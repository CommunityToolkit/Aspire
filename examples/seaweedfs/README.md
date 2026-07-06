# SeaweedFS Aspire Integration Example

This example demonstrates how to integrate and consume a SeaweedFS cluster within a .NET Aspire distributed application. It showcases both the **S3-Compatible API** and the **Native Filer API** in a single cohesive setup.

## Project Structure

* **SeaweedFS.AppHost:** The Aspire orchestrator. It spins up the SeaweedFS Docker container, enables the S3 Gateway and Data Volumes, and injects the dynamic connection strings into the API.
* **SeaweedFS.ServiceDefaults:** Standard Aspire telemetry, resilience, and health check configurations.
* **SeaweedFS.ApiService:** A minimal API application that registers both the `IAmazonS3` client and the `SeaweedFSFilerClient` to interact with the storage cluster.

## Running the Example

1. Ensure you have [Docker Desktop](https://www.docker.com/products/docker-desktop/) or Podman running on your machine.
2. Open a terminal in this directory (`aspire/seaweedfs/`).
3. Run the AppHost:

```dotnetcli
dotnet run --project SeaweedFS.AppHost

```

4. Open the **Aspire Dashboard** URL provided in the console output.
5. Wait for both the `seaweedfs` container and the `apiservice` to show as **Healthy**.

## Exploring the Endpoints

The `SeaweedFS.ApiService` exposes endpoints to test both storage approaches. You can trigger them using Swagger (if enabled) or using tools like `curl` or Postman.

### S3 Endpoints (AWS SDK Compatibility)

These endpoints use the injected `IAmazonS3` client.

* **Create a Bucket:**
`POST /s3/buckets?bucketName=my-bucket`
* **Upload a File:**
`POST /s3/upload?bucketName=my-bucket&key=test.txt`
*(Send raw text in the body)*
* **Download a File:**
`GET /s3/download?bucketName=my-bucket&key=test.txt`

### Filer Endpoints (Native SeaweedFS API)

These endpoints use the injected `SeaweedFSFilerClient` performing direct HTTP calls to the cluster.

* **Upload a File to Root:**
`POST /filer/upload?fileName=native-file.txt`
*(Send raw text in the body)*
* **List Directory Contents:**
`GET /filer/list`
*(Returns a JSON representation of the Filer's directory structure)*

## Viewing Data in SeaweedFS

Since `SeaweedFS.AppHost` maps a data volume using `.WithDataVolume()`, any file you upload using the endpoints above will be persisted in the Docker volume. If you stop the AppHost and run it again, your data will still be accessible.