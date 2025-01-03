# CommunityToolkit.Aspire.Hosting.Ngrok library

Provides extension methods and resource definitions for a .NET Aspire AppHost to configure a ngrok container.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Ngrok
```

### Example usage

Then, in the _Program.cs_ file of app host, add a ngrok resource and add endpoints to be tunneled following methods:

```csharp
var myService = builder.AddProject<Projects.MyService>();
var otherSevice = builder.AddProject<Projects.OtherService>();

var authToken = builder
    .AddParameter("ngrok-auth-token", "your-ngrok-auth-token", secret: true);

builder.AddNgrok("ngrok")
    .WithAuthToken(authToken)
    .WithTunnelEndpoint(myService, "http", "<your-ngrok-domain>")
    .WithTunnelEndpoint(otherSevice, "http"); // ngrok will generate a random domain for this service
```

## Additional Information

https://ngrok.com
https://learn.microsoft.com/dotnet/aspire/community-toolkit/ngrok

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire