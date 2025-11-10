# CommunityToolkit.Aspire.Hosting.Python.Extensions library

> **⚠️ DEPRECATION NOTICE**  
> This package is deprecated as of Aspire 13.0. The functionality provided by this package is now part of the core `Aspire.Hosting.Python` package.
>
> **Migration Guide:**
> - Replace `AddUvicornApp()` calls with `Aspire.Hosting.Python.PythonAppResourceBuilderExtensions.AddUvicornApp()`
> - Replace `AddUvApp()` calls with `AddPythonApp().WithUvEnvironment()`
> - Update resource type references from `CommunityToolkit.Aspire.Hosting.Python.Extensions.UvicornAppResource` to `Aspire.Hosting.ApplicationModel.UvicornAppResource`
>
> This package will be removed in a future release. Please migrate your applications to use the core `Aspire.Hosting.Python` package.

Provides extensions methods and resource definitions for the .NET Aspire AppHost to extend the support for Python applications. Current support includes:
- Uvicorn
- Uv

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Python.Extensions
```

### Initialize the Python virtual environment

Please refer to the [Python virtual environment](https://learn.microsoft.com/dotnet/aspire/get-started/build-aspire-apps-with-python?tabs=powershell#initialize-the-python-virtual-environment) section for more information.

### Uvicorn example usage

Then, in the _Program.cs_ file of `AddUvicornApp`, define a Uvicorn resource, then call `Add`:

```csharp
var uvicorn = builder.AddUvicornApp("uvicornapp", "../uvicornapp-api", "main:app")
    .WithHttpEndpoint(env: "UVICORN_PORT");
```

### Uv example usage

Then, in the _Program.cs_ file of `AddUvApp`, define a Uvicorn resource, then call `Add`:

```csharp
var uvicorn = builder.AddUvApp("uvapp", "../uv-api", "uv-api")
    .WithHttpEndpoint(env: "PORT");
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-python-extensions

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

