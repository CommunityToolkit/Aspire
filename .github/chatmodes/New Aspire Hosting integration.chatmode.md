---
description: "This chat mode is to create a new .NET Aspire hosting integration, using the design in the Community Toolkit repo."
tools: [
        "changes",
        "codebase",
        "editFiles",
        "fetch",
        "new",
        "problems",
        "runCommands",
        "runTasks",
        "search",
        "searchResults",
        "usages",
    ]
---

# New Aspire Hosting Integration

You are going to develop a new .NET Aspire hosting integration. The following in the process in which you need to go through to complete the task.

## 1. Collect Requirements

You are going to need to know:

-   The name of the hosting integration.
-   The description of the hosting integration.

This will be important to ensure that the hosting integration is created correctly.

Ideally, the user should provide a URL for the docs on how to run the tool that the hosting integration is for, using the `fetch` tool, so that you can use it to understand how the tool works and how to implement the hosting integration.

## 2. Scaffold the C# project

Start by creating a new C# class library project in the `src` folder. The project should be named `CommunityToolkit.Aspire.Hosting.<HostingIntegrationName>`, where `<HostingIntegrationName>` is the name of the hosting integration you are creating.

It can be created using the following command:

```bash
dotnet new classlib -n CommunityToolkit.Aspire.Hosting.<HostingIntegrationName> -o src/CommunityToolkit.Aspire.Hosting.<HostingIntegrationName>
```

Once created the `csproj` file can be stripped back to just the following minimal starting point:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <AdditionalPackageTags>hosting $HostingIntegrationName</AdditionalPackageTags>
    <Description>$HostingIntegrationDescription.</Description>
  </PropertyGroup>

</Project>
```

Also create an empty `README.md` file in the root of the project with the following content:

```markdown
# CommunityToolkit.Aspire.Hosting.<HostingIntegrationName>

TODO: Add a description of the hosting integration.
```

Ensure that the project is added to the solution file, which is in the repo root, and can be done using:

```bash
dotnet sln add src/CommunityToolkit.Aspire.Hosting.<HostingIntegrationName>
```

## 3. Implement the Hosting Integration

An integration consists of two main parts, a `Resource` implementation, and extension methods for adding it to the `IDistributedApplicationBuilder`.

### 3.1 Create the Resource

There are multiple choices for the `Resource` implementation. Using the knowledge of the thing to be hosted, you can choose the most appropriate from the list:

-   `ExecutableResource` - For running a local executable (e.g. Node.js, Python, Rust, etc.)
-   `ContainerResource` - For running a container image using Docker.
-   `Resource` - For running a generic resource that does not fit into the above categories.

Here is an example of how to implement a `ContainerResource`:

```csharp
namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents an Ollama container.
/// </summary>
/// <remarks>
/// Constructs an <see cref="OllamaResource"/>.
/// </remarks>
/// <param name="name">The name for the resource.</param>
public class OllamaResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string OllamaEndpointName = "http";

    private readonly List<string> _models = [];

    private EndpointReference? _primaryEndpointReference;

    /// <summary>
    /// Adds a model to the list of models to download on initial startup.
    /// </summary>
    public IReadOnlyList<string> Models => _models;

    /// <summary>
    /// Gets the endpoint for the Ollama server.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpointReference ??= new(this, OllamaEndpointName);

    /// <summary>
    /// Gets the connection string expression for the Ollama server.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
      ReferenceExpression.Create(
        $"Endpoint={PrimaryEndpoint.Property(EndpointProperty.Scheme)}://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}"
      );

    /// <summary>
    ///     Adds a model to the list of models to download on initial startup.
    /// </summary>
    /// <param name="modelName">The name of the model</param>
    public void AddModel(string modelName)
    {
        ArgumentException.ThrowIfNullOrEmpty(modelName, nameof(modelName));
        if (!_models.Contains(modelName))
        {
            _models.Add(modelName);
        }
    }
}
```

Resources can also have endpoints, which are used to connect to the resource, they can also expose a connection string expression, which is used to connect to the resource in a more generic way.

The following requirements **must** be met when implementing a resource:

-   Namespace: `Aspire.Hosting.ApplicationModel`.
-   Class name: `<HostingIntegrationName>Resource`.
-   Inherits from `Resource`, `ContainerResource`, or `ExecutableResource`.
-   Public constructor that takes a `string name` parameter.
-   If the resource has a connection string, it must implement `IResourceWithConnectionString`.
-   If the resource has endpoints, it must implement `IResourceWithEndpoints`.
-   Public methods, properties, and events should be documented with XML comments.

### 3.2 Create the Extension Methods

Next, you need to create extension methods for the `IDistributedApplicationBuilder` to add the resource to the application. This is done by creating a static class with a method that takes an `IDistributedApplicationBuilder` and returns an `IResourceBuilder<T>`.

Here is an example of how to implement the extension method for the `OllamaResource`:

```csharp
namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding a Bun app to a <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class BunAppExtensions
{
    /// <summary>
    /// Adds a Bun app to the builder.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="workingDirectory">The working directory.</param>
    /// <param name="entryPoint">The entry point, either a file or package.json script name.</param>
    /// <param name="watch">Whether to watch for changes.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<BunAppResource> AddBunApp(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string? workingDirectory = null,
        string entryPoint = "index.ts",
        bool watch = false)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
        ArgumentException.ThrowIfNullOrEmpty(entryPoint, nameof(entryPoint));

        workingDirectory ??= Path.Combine("..", name);

        var resource = new BunAppResource(name, PathNormalizer.NormalizePathForCurrentPlatform(Path.Combine(builder.AppHostDirectory, workingDirectory)));

        string[] args = watch ? ["--watch", "run", entryPoint] : ["run", entryPoint];

        return builder.AddResource(resource)
            .WithBunDefaults()
            .WithArgs(args);
    }
}
```

The extension method should meet the following requirements:

-   Namespace: `Aspire.Hosting`.
-   Class name: `<HostingIntegrationName>Extensions`.
-   Static class with a static method.
-   Method name: `Add<HostingIntegrationName>`.
-   Method parameters:
    -   `IDistributedApplicationBuilder builder` - The builder to add the resource to.
    -   `string name` - The name of the resource, decorated with `[ResourceName]`.
    -   Additional parameters as required by the resource.
-   Returns an `IResourceBuilder<T>` where `T` is the resource type.
-   The method should call `builder.AddResource(resource)` to add the resource to the builder.
-   Perform `ArgumentNullException.ThrowIfNull` and `ArgumentException.ThrowIfNullOrEmpty` checks on the parameters.

## 4. Sample Usage

You need to create a sample usage of the hosting integration in the `examples` folder. This should be a minimal example that demonstrates how to use the hosting integration in a .NET Aspire application.

Start by scaffolding a new .NET Aspire App Host project in the `examples` folder. This can be done using the following command:

```bash
dotnet new aspire-apphost -n CommunityToolkit.Aspire.Hosting.<HostingIntegrationName>.AppHost -o examples/CommunityToolkit.Aspire.Hosting.<HostingIntegrationName>.AppHost
```

Make sure to add the project to the solution with `dotnet sln add examples/CommunityToolkit.Aspire.Hosting.<HostingIntegrationName>.AppHost`.

Once created, refer to existing AppHost `csproj` files to ensure that the right packages are referenced, such as `CommunityToolkit.Aspire.Hosting.<HostingIntegrationName>`. For the `Sdk`, ensure the version is `$(AspireAppHostSdkVersion)`, since we use a MSBuild variable to ensure that a consistent version is used across all App Host projects. Any `PackageReference` elements should **not** have a version specified.

Next, edit the `AppHost.cs` file that the template created to use the hosting integration.

Here is an example of how to use the `BunResource` in the `AppHost.cs` file:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddBunApp("api")
    .WithBunPackageInstallation()
    .WithHttpEndpoint(env: "PORT")
    .WithHttpHealthCheck("/");

builder.Build().Run();
```

Ensure that the example is a minimal working example that can be run using the `dotnet run` command.

## 5. Tests

You need to create tests for the hosting integration. This should include unit tests for the resource implementation and integration tests for the extension methods.

The tests should be placed in a new test project in the `tests` folder. The project should be named `CommunityToolkit.Aspire.Hosting.<HostingIntegrationName>.Tests`, and can be created using the following command:

```bash
dotnet new xunit -n CommunityToolkit.Aspire.Hosting.<HostingIntegrationName>.Tests -o tests/CommunityToolkit.Aspire.Hosting.<HostingIntegrationName>.Tests
```

Make sure to add the project to the solution with `dotnet sln add examples/CommunityToolkit.Aspire.Hosting.<HostingIntegrationName>.AppHost`.

Ensure that the test project references the hosting integration project and any necessary Aspire packages. Refer to other test projects in the `tests` folder for guidance on how to set up the project file.

Once the project is created, you can start writing tests for the resource implementation and extension methods. Ensure that the tests cover all public methods and properties of the resource, as well as the extension methods.

## 6. Documentation

Once the integration is implemented, you need to update the `README.md` file in the hosting integration project to include a description of the hosting integration, how to use it, and any other relevant information.

Also, update the root `README.md` file of the repo to include a link to the new hosting integration in the table that exists.
