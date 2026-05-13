# CommunityToolkit.Aspire.Hosting.Solr

This package provides an Aspire hosting integration for [Apache Solr](https://solr.apache.org/), enabling you to add and configure a Solr container as part of your distributed application.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Solr
```

## Usage Example

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Add Solr resource with default settings (port 8983, core "solr")
var solr = builder.AddSolr("solr");

// Add Solr with custom port
var solrWithCustomPort = builder.AddSolr("solr-custom", port: 8984);

// Add Solr with custom core name
var solrWithCustomCore = builder.AddSolr("solr-core", coreName: "mycore");

// Add Solr with both custom port and core name
var solrCustom = builder.AddSolr("solr-full", port: 8985, coreName: "documents");

// Reference the Solr resource in a project
var exampleProject = builder.AddProject<Projects.ExampleProject>()
                            .WithReference(solr);

// Initialize and run the application
builder.Build().Run();
```

## Using a Custom Config Set

You can configure Solr to use a custom config set by calling `WithConfigset` on the Solr resource builder. This mounts a local config set directory into the container and creates the core using that config set.

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Add Solr with a custom config set
var solr = builder.AddSolr("solr", coreName: "mycore")
                  .WithConfigset("my-config", "/path/to/configsets/my-config");

builder.Build().Run();
```

The `WithConfigset` method takes two parameters:

- `configSetName` — the name of the config set as it will appear inside the container (under `/opt/solr/server/solr/configsets/`)
- `configSetPath` — the absolute path on the host to the config set directory to mount

When a config set is provided, the core is created with `solr-create -c <coreName> -d <configSetName>` instead of the default `solr-precreate <coreName>`.

## Persisting Data

### Using a Named Volume

Use `WithDataVolume` to persist Solr data across container restarts with a Docker named volume:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Use an auto-generated volume name
var solr = builder.AddSolr("solr")
                  .WithDataVolume();

// Or specify a custom volume name
var solrNamed = builder.AddSolr("solr")
                       .WithDataVolume("my-solr-data");

builder.Build().Run();
```

### Using a Bind Mount

Use `WithDataBindMount` to mount a host directory into the container for data storage:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var solr = builder.AddSolr("solr")
                  .WithDataBindMount("/path/to/solr/data");

builder.Build().Run();
```

The `WithDataBindMount` method mounts the specified host path to `/var/solr` inside the container. The directory must be writable by the Solr process (UID 8983).

## Feedback & contributing

https://github.com/dotnet/aspire
