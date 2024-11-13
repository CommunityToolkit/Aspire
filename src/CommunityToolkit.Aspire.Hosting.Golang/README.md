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

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-golang

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

