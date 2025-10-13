# CommunityToolkit.Aspire.Hosting.Neon library

This integration provides support for [Neon](https://neon.tech/), a serverless PostgreSQL-compatible database, in .NET Aspire applications.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Neon
```

### Example usage

In the _Program.cs_ file of your AppHost project, add a Neon project and database:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Add Neon project resource with branching support
var neon = builder.AddNeonProject("neon");

// Add a Neon database
var neonDb = neon.AddDatabase("neondb");

// Reference the Neon database in a project
var exampleProject = builder.AddProject<Projects.ExampleProject>()
                            .WithReference(neonDb);

builder.Build().Run();
```

## Additional Configuration

### Data Persistence

To persist data across container restarts, you can use either a volume or bind mount:

```csharp
var neon = builder.AddNeonProject("neon")
    .WithDataVolume(); // or .WithDataBindMount("./data/neon")
```

### Custom Credentials

You can provide custom credentials for the Neon project:

```csharp
var userName = builder.AddParameter("neon-user");
var password = builder.AddParameter("neon-password", secret: true);

var neon = builder.AddNeonProject("neon", userName, password);
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-neon

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
