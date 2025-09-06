# CommunityToolkit.Aspire.Hosting.Solr

This package provides a .NET Aspire hosting integration for [Apache Solr](https://solr.apache.org/), enabling you to add and configure a Solr container as part of your distributed application.

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

## Feedback & contributing

https://github.com/dotnet/aspire
