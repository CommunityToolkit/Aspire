# CommunityToolkit.Aspire.Hosting.Python.Extensions library

Provides extensions methods and resource definitions for the .NET Aspire AppHost to extend the support for Python applications. Current support includes:
- Uvicorn
- Uv
- Streamlit

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

### Streamlit example usage

Then, in the _Program.cs_ file of `AddStreamlitApp`, define a Streamlit resource, then call `Add`:

```csharp
var streamlit = builder.AddStreamlitApp("streamlitapp", "../streamlit-api", "app.py")
    .WithHttpEndpoint(env: "PORT");
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-python-extensions

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

