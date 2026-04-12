# CommunityToolkit.Aspire.Hosting.Compose

An Aspire hosting integration that imports existing Docker Compose files into the Aspire resource graph. Compose services become first-class citizens with full support for `WaitFor`, dependency graph, and dashboard visibility.

## Getting Started

### Install the NuGet package

```bash
dotnet add package CommunityToolkit.Aspire.Hosting.Compose
```

### Runtime usage (string-based)

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var infra = builder.AddCompose(".infra/compose.yml");

builder.AddProject<Projects.MyApi>("api")
    .WaitFor(infra["postgres"])
    .WaitFor(infra["redis"]);

builder.Build().Run();
```

### Typed usage with source generator

Register compose files in your AppHost `.csproj`:

```xml
<ItemGroup>
    <ComposeReference Include=".infra/compose.yml" Name="Infra" />
    <ComposeReference Include=".infra/compose.monitoring.yml" Name="Monitoring" />
</ItemGroup>
```

Then use generated types with IntelliSense:

```csharp
var infra = builder.AddCompose<Compose.Infra>();
var mon = builder.AddCompose<Compose.Monitoring>();

builder.AddProject<Projects.MyApi>("api")
    .WaitFor(infra[Compose.Infra.Postgres])
    .WaitFor(mon[Compose.Monitoring.Grafana]);
```

### Multiple compose files

```csharp
var infra = builder.AddCompose(".infra/compose.yml");
var monitoring = builder.AddCompose(".infra/compose.monitoring.yml");
```

### Supported compose versions

All Docker Compose file formats are supported: v1 (legacy), v2.x, v3.x, and modern Compose Spec.

## Additional Information

- [Aspire Community Toolkit GitHub Repository](https://github.com/CommunityToolkit/Aspire)
