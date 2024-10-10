## 💡 Creating an Integration

So, you want to create a new integration for the .NET Aspire Community Toolkit? That's awesome! This guide will walk you through the process of creating a new integration.

First up, make sure you've had a read of the [Contributing guide](../CONTRIBUTING.md) to understand the contribution guidelines, and then familiarise yourself with creating a custom .NET Aspire integration for [hosting](https://learn.microsoft.com/dotnet/aspire/extensibility/custom-hosting-integration?tabs=windows) or for [clients](https://learn.microsoft.com/dotnet/aspire/extensibility/custom-client-integration).

## 📂 Repository Structure

The repository structure for the .NET Aspire Community Toolkit is as follows:

-   `src/` - Contains the source code for the toolkit and where you'll create your integration.
-   `tests/` - Contains the test projects for the toolkit and where you'll create unit and integration tests (where applicable).
-   `examples/` - Contains example projects that demonstrate how to use the integrations.

## 🛠️ Setting up your Development Environment

The recommended development environment for contributing to the .NET Aspire Community Toolkit is [VS Code](https://code.visualstudio.com/) using the [`devcontainer`](https://code.visualstudio.com/docs/remote/containers) extension. This will ensure that you have all the necessary tools and dependencies installed to build and test the toolkit.

### Manual Setup

If you prefer not to use `devcontainer`, you can manually set up your development environment by installing the following tools:

-   [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0)
-   [Node.js LTS](https://nodejs.org/en/)
-   [Java JDK 11](https://learn.microsoft.com/java/openjdk/download)
-   [Docker](https://docs.docker.com/get-docker/)
    -   Podman is also supported, but Docker is recommended for consistency.

And of course, an editor such as Visual Studio, JetBrains Rider or emacs.

## 🚀 Getting Started

To create a new integration, you'll need to create a new project in the `src/` directory. The project needs to be prefixed with `Aspire.CommunityToolkit.` and should be named after the integration you're creating, using the naming guidelines from the .NET Aspire team. For example, if you're creating a **Hosting** integration, then the project name should be `Aspire.CommunityToolkit.Hosting.MyIntegration`, whereas if you're creating a **Client** integration, then the project name should be `Aspire.CommunityToolkit.MyIntegration`.

> Note: All integration packages will have the `Aspire.Hosting` NuGet package added as a dependency, as well as some standard MSBuild properties. You can see what is pre-configured in the `Directory.Build.props` file.

## 🔎 Namespaces

To improve discovery of the integration when someone consumes the NuGet package, extension methods for adding the integration should be placed in the `Aspire.Hosting` for hosting integrations or `Microsoft.Extensions.Hosting` for client integrations. For custom resources created in hosting integrations, use the `Aspire.Hosting.ApplicationModel` namespace.

If your integration will be pulling a container image from a registry, you should specify a specific tag for the image in a `major.minor` format to pull, and not use the `latest` tag. This will ensure that the integration is stable and not affected by changes to the image. If the image is not versioned, you should use the `sha256` digest of the image.
## 🪧 Example application

To demonstrate how to use the integration, you should create an example application in the `examples/` directory. This should be a simple application that demonstrates the minimal usage of the integration. At minimum there will need to be an AppHost project which uses the integration. This example application will also be used in the integration tests if it's a hosting integration.

## 🧪 Testing

The testing framework used is [`xunit`](https://xunit.net/), and you'll need to create a new test project in the `tests/` directory. The test project should be named `Aspire.CommunityToolkit.Hosting.MyIntegration.Tests` or `Aspire.CommunityToolkit.MyIntegration.Tests` following the same naming guidelines as the integration project. It's easiest to create a **Class Library** project as the `Directory.Build.props` will automatically add the necessary test dependencies.

Asserts can be written using the `Assert` type or `FluentAssertions` if that is your preferred style, both are supported.

### Unit Tests

Unit tests should be created to test the functionality of the publicly exposed methods and classes in the integration. The `DistributedApplication.CreateBuilder` method should be used to create a stub AppHost builder for testing.

Here's an example test:

```csharp
[Fact]
public void DefaultViteAppUsesNpm()
{
    var builder = DistributedApplication.CreateBuilder();

    builder.AddViteApp("vite");

    using var app = builder.Build();

    var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

    var resource = appModel.Resources.OfType<NodeAppResource>().SingleOrDefault();

    Assert.NotNull(resource);

    Assert.Equal("npm", resource.Command);
}
```

### Integration Tests

If you are creating a hosting integration, it's important to add integration tests that verify the end-to-end functionality of the integration. To do this, add a project reference to your example AppHost project and then inherit your test class from `IClassFixture<AspireIntegrationTestFixture<TExampleAppHost>>` which will inject a `AspireIntegrationTestFixture<TExampleAppHost>` into the constructor of your test class. You can learn more about ClassFixtures in the [xUnit documentation](https://xunit.net/docs/shared-context#class-fixture).

The `AspireIntegrationTestFixture<TExampleAppHost>` object exposes methods such as `CreateHttpClient` to get access to a `HttpClient` that can be used to make requests to the integrations HTTP endpoints (if applicable). It also exposes the `App` property which provides access to the `DistributedApplication` for more advanced testing scenarios.

Here's an example integration test:

```csharp
[Theory]
[InlineData("vite-demo")]
[InlineData("yarn-demo")]
[InlineData("pnpm-demo")]
public async Task ResourceStartsAndRespondsOk(string appName)
{
    var httpClient = fixture.CreateHttpClient(appName);

    await fixture.App.WaitForTextAsync("VITE", appName).WaitAsync(TimeSpan.FromSeconds(30));

    var response = await httpClient.GetAsync("/");

    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

#### Docker and CI

GitHub Actions Windows runners do not support Linux container images, so if your integration will require a container image it needs to be marked with a `[RequiresDocker]` attribute. This will dynamically add a _Trait_ (category) to the test that can be used to filter tests in the CI pipeline.

#### Waiting for the resource to start

If the resource your integration exposes does not integrate into the .NET Aspire health check model, you may need to parse its logs to determine when it is ready to accept requests. To do this, use the `WaitForTextAsync` extension method on the `DistributionApplication` object to wait for a specific message to appear in the logs. Do note that this method is marked with `CTASPIRE001` so you will need to disable that warning where you use it. You can learn more about `CTASPIRE001` in the [Diagnostics documentation](./diagnostics.md).

## 📃 Documentation

You'll need to add a `README.md` file to the folder your integration is created in, this will be used in the NuGet package that is generated. This should be a high level overview of the integration and does not need to be a complete doc set.

For the complete docs, you'll need to create a PR to the [`dotnet/docs-aspire`](https://github.com/dotnet/docs-aspire) repository, under the `docs/community-toolkit` folder. This will be reviewed by the docs owners and merged into the main docs for .NET Aspire. Also, remember to update the `docs/fundamentals/overview.md` in that repo with the new integration.

Lastly, update the `README.md` in the root of this repository to include your new integration in the table of integrations.

## 📦 NuGet Packaging

Your integration will be automatically packaged as a NuGet package when a PR is created and added as an artifact to the CI job (assuming it builds!), allowing you to test it in other projects while it is being reviewed. Once reviewed it will move through the release workflow that the maintainers have set up. You can learn more about that in the [versioning documentation](./versioning.md).

## 🎉 You're done

That's it! You've created a new integration for the .NET Aspire Community Toolkit. If you have any questions or need help, feel free to reach out to the maintainers or the community on GitHub Discussions.

