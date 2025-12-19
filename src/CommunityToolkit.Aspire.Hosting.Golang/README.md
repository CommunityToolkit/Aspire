# CommunityToolkit.Aspire.Hosting.Golang library

Provides extensions methods and resource definitions for the .NET Aspire AppHost to support running Golang applications.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Golang
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define a Golang resource, then call `AddGolangApp`:

```csharp
var golang = builder.AddGolangApp("golang", "../gin-api")
    .WithHttpEndpoint(env: "PORT");
```

The `PORT` environment variable is used to determine the port the Golang application should listen on. It is randomly assigned by the .NET Aspire. The name of the environment variable can be changed by passing a different value to the `WithHttpEndpoint` method.

To have the Golang application listen on the correct port, you can use the following code in your Golang application:

```go
r.Run(":"+os.Getenv("PORT"))
```

## Dependency Management

The integration provides support for Go module dependency management using `go mod tidy` or `go mod download`.

### Using `go mod tidy`

To run `go mod tidy` before your application starts (to clean up and verify dependencies):

```csharp
var golang = builder.AddGolangApp("golang", "../gin-api")
    .WithGoModTidy()
    .WithHttpEndpoint(env: "PORT");
```

### Using `go mod download`

To run `go mod download` before your application starts (to download dependencies without verification):

```csharp
var golang = builder.AddGolangApp("golang", "../gin-api")
    .WithGoModDownload()
    .WithHttpEndpoint(env: "PORT");
```

Both methods create an installer resource that runs before your application starts, ensuring dependencies are available. The installer resource appears as a child resource in the Aspire dashboard.

You can also customize the installer resource using the optional `configureInstaller` parameter:

```csharp
var golang = builder.AddGolangApp("golang", "../gin-api")
    .WithGoModTidy(configureInstaller: installer =>
    {
        installer.WithEnvironment("GOPROXY", "https://proxy.golang.org,direct");
    })
    .WithHttpEndpoint(env: "PORT");
```

> **Note:** The `WithGoModTidy` and `WithGoModDownload` methods only run during development. When publishing, the generated Dockerfile handles dependency management automatically.

## Publishing

When publishing your Aspire application, the Golang resource automatically generates a multi-stage Dockerfile for containerization. This means you don't need to manually create a Dockerfile for your Golang application.

### Automatic Version Detection

The integration automatically detects the Go version to use by:
1. Checking the `go.mod` file for the Go version directive
2. Falling back to the installed Go toolchain version
3. Using Go 1.23 as the default if no version is detected

### Customizing Base Images

You can customize the base images used in the Dockerfile:

```csharp
var golang = builder.AddGolangApp("golang", "../gin-api")
    .WithHttpEndpoint(env: "PORT")
    .WithDockerfileBaseImage(
        buildImage: "golang:1.22-alpine",
        runtimeImage: "alpine:3.20");
```

### Generated Dockerfile

The automatically generated Dockerfile:
- Uses the detected or default Go version (e.g., `golang:1.22`) as the build stage
- Uses `alpine:3.21` as the runtime stage for a smaller final image
- Installs CA certificates in the runtime image for HTTPS support
- Respects your build tags if specified
- Builds the executable specified in your `AddGolangApp` call

This automatic Dockerfile generation happens when you publish your Aspire application and requires no additional configuration.

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-golang

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

