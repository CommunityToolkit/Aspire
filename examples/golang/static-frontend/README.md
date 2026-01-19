# Static Frontend Example

This directory contains a simple static HTML frontend that demonstrates the container files feature of the Golang integration.

## Purpose

When publishing an Aspire application, static files from this directory can be copied into the Golang container using the `WithContainerFiles` method. This is useful for serving frontend assets (HTML, CSS, JavaScript, images, etc.) directly from your Go application.

## Usage

In your AppHost `Program.cs`, you can configure the Golang resource to copy these files:

```csharp
var golang = builder.AddGolangApp("golang", "../gin-api")
    .WithHttpEndpoint(env: "PORT")
    .WithContainerFiles("/app/static", "../static-frontend");
```

The Golang application is configured to serve these files from the `/static` endpoint. When running in a container, the files will be available at `http://localhost:<port>/static/index.html`.

## Container Files Feature

The Golang resource implements `IContainerFilesDestinationResource`, which tells the Aspire publishing pipeline where to copy files when building the container image. In this example, `/app/static` is used as the conventional destination (and is returned by the `ContainerFilesDestination` property), but the actual destination is determined by the first parameter you pass to `WithContainerFiles` and can be customized.

This feature works with any resource that produces output files, including:
- Static file directories (like this example)
- Frontend build tools (Vite, webpack, Create React App, etc.)
- Documentation generators
- Any other build output

## Implementation Details

The feature works by:
1. The Golang resource implementing `IContainerFilesDestinationResource`
2. Using `WithContainerFiles` to specify the source and destination
3. During publishing, the Aspire pipeline automatically copies the files into the generated Docker image
4. The Golang application serves the files using Gin's `Static` method

See the main README in the parent directory for more details.
