# CommunityToolkit.Aspire.GoFeatureFlag

Registers a [GoFeatureFlagProvider](https://github.com/open-feature/dotnet-sdk-contrib/tree/main/src/OpenFeature.Contrib.Providers.GOFeatureFlag) in the DI container for connecting to a GO Feature Flag instance.

## Getting started

### Install the package

Install the .NET Aspire GO Feature Flag Client library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.GoFeatureFlag
```

## Usage example

In the _Program.cs_ file of your project, call the `AddGoFeatureFlagClient` extension method to register a `GoFeatureFlagProvider` for use via the dependency injection container. The method takes a connection name parameter.

```csharp
builder.AddGoFeatureFlagClient("goff");
```

## Configuration

The .NET Aspire GO Feature Flag Client integration provides multiple options to configure the server connection based on the requirements and conventions of your project.

### Use a connection string

When using a connection string from the `ConnectionStrings` configuration section, you can provide the name of the connection string when calling `builder.AddGoFeatureFlagClient()`:

```csharp
builder.AddGoFeatureFlagClient("goff");
```

And then the connection string will be retrieved from the `ConnectionStrings` configuration section:

```json
{
    "ConnectionStrings": {
        "goff": "Endpoint=http://localhost:19530/"
    }
}
```

### Use configuration providers

The .NET Aspire GO Feature Flag Client integration supports [Microsoft.Extensions.Configuration](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration). It loads the `GoFeatureFlagClientSettings` from configuration by using the `Aspire:GoFeatureFlag:Client` key. Example `appsettings.json` that configures some of the options:

```json
{
    "Aspire": {
        "GoFeatureFlag": {
            "Client": {
                "Endpoint": "http://localhost:19530/",
                "MasterKey": "123456!@#$%"
            }
        }
    }
}
```

### Use inline delegates

Also you can pass the `Action<GoFeatureFlagClientSettings> configureSettings` delegate to set up some or all the options inline, for example to set the API key from code:

```csharp
builder.AddGoFeatureFlagClient("goff", settings => settings.ProviderOptions.ApiKey = "123456!@#$%");
```

## AppHost extensions

In your AppHost project, install the `CommunityToolkit.Aspire.Hosting.GoFeatureFlag` library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.GoFeatureFlag
```

Then, in the _Program.cs_ file of `AppHost`, register a GO Feature Flag instance and consume the connection using the following methods:

```csharp
var goff = builder.AddGoFeatureFlag("goff");

var myService = builder.AddProject<Projects.MyService>()
                       .WithReference(goff);
```

The `WithReference` method configures a connection in the `MyService` project named `goff`. In the _Program.cs_ file of `MyService`, the GO Feature Flag connection can be consumed using:

```csharp
builder.AddGoFeatureFlagClient("goff");
```

Then, in your service, inject `GoFeatureFlagProvider` and use it to interact with the GO Feature Flag API:

```csharp
public class MyService(GoFeatureFlagProvider goFeatureFlagProvider)
{
    // ...
}
```

## Additional documentation

-   https://github.com/thomaspoignant/go-feature-flag
-   https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-go-feature-flag

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
