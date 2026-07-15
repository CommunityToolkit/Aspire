# CommunityToolkit.Aspire.Logto.Client

Registers [Logto](https://logto.io/) authentication services in the DI container for connecting to a Logto instance using OpenID Connect or JWT Bearer authentication.

## Getting started

### Prerequisites

- A Logto instance (can be hosted via `CommunityToolkit.Aspire.Hosting.Logto`).

### Install the package

Install the Aspire Logto Client library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Logto.Client
```

## Usage example

### OpenID Connect (OIDC) Authentication

In the _Program.cs_ file of your project, call the `AddLogtoOIDC` extension method to register Logto OIDC authentication for use via the dependency injection container. The method takes an optional connection name parameter.

```csharp
builder.AddLogtoOIDC("logto");
```

### JWT Bearer Authentication

For API projects that need to validate Logto-issued JWTs, use the `AddLogtoJwtBearer` extension method on the `AuthenticationBuilder`:

```csharp
builder.Services
    .AddAuthentication()
    .AddLogtoJwtBearer("logto", "your-app-identifier");
```

## Configuration

The Aspire Logto Client integration provides multiple options to configure the server connection based on the requirements and conventions of your project.

### Use a connection string

When using a connection string from the `ConnectionStrings` configuration section, you can provide the name of the connection string when calling `builder.AddLogtoOIDC()`:

```csharp
builder.AddLogtoOIDC("logto");
```

And then the connection string will be retrieved from the `ConnectionStrings` configuration section:

```json
{
    "ConnectionStrings": {
        "logto": "Endpoint=http://localhost:3001"
    }
}
```

### Use configuration providers

The Aspire Logto Client integration supports [Microsoft.Extensions.Configuration](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration). It loads the settings from configuration by using the `Aspire:Logto:Client` key. Example `appsettings.json` that configures some of the options:

```json
{
  "Aspire": {
    "Logto": {
      "Client": {
        "Endpoint": "http://localhost:3001",
        "AppId": "your-app-id",
        "AppSecret": "your-app-secret"
      }
    }
  }
}
```

All properties exposed by `LogtoOptions`, including scopes, resource, prompt, callback paths, user-info claims, and cookie domain, can be configured in this section or through the inline delegate. The endpoint must be an absolute HTTP or HTTPS URI.

### Use inline delegates

You can also pass the `Action<LogtoOptions>` delegate to set up some or all the options inline, for example to set the AppId from code:

```csharp
builder.AddLogtoOIDC("logto", logtoOptions: options =>
{
    options.AppId = "your-app-id";
    options.AppSecret = "your-app-secret";
});
```

## AppHost extensions

In your AppHost project, install the `CommunityToolkit.Aspire.Hosting.Logto` library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Logto
```

Then, in the _Program.cs_ file of `AppHost`, register a Logto instance and consume the connection using the following methods:

```csharp
var postgres = builder.AddPostgres("postgres");

var logto = builder.AddLogto("logto", postgres);

var myService = builder.AddProject<Projects.MyService>()
                       .WithReference(logto);
```

The `WithReference` method configures a connection in the `MyService` project named `logto`. In the _Program.cs_ file of `MyService`, the Logto connection can be consumed using:

```csharp
builder.AddLogtoOIDC("logto");
```

## Additional documentation

- https://logto.io/docs
- https://github.com/logto-io/csharp

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
