# CommunityToolkit.Aspire.Hosting.Solr

This package provides a .NET Aspire hosting integration for Apache Solr, enabling you to add and configure a Solr container as part of your distributed application.

## Features
- Starts the official Solr Docker image as a container resource
- Allows configuration of the host port
- Supports adding Solr cores as child resources
- Provides connection string references for Solr cores
- Supports data volumes and bind mounts through standard Aspire extension methods

## Usage Example

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Add Solr resource
var solr = builder.AddSolr("solr");

// Set Port Number (default is 8983)
var solrWithCustomPort = builder.AddSolr("solr-custom", port: 8984);

// Add a Solr Core
var solrCore = solr.AddSolrCore("solrcore");

// Reference the Solr Core in a project
var exampleProject = builder.AddProject<Projects.ExampleProject>()
                            .WithReference(solrCore);

// Initialize and run the application
builder.Build().Run();
```

## Configuration
- **AddSolr(string name, int? port = null):** Add a Solr container with optional custom port (default: 8983)
- **AddSolrCore(string coreName):** Add a Solr core to an existing Solr resource
- Standard Aspire extension methods like **WithDataVolume()** and **WithDataBindMount()** are available

## Connection Strings
- The main Solr resource provides a connection string to the Solr server base URL
- Solr core resources provide connection strings to the specific core endpoint (e.g., `http://localhost:8983/solr/corename`)

## License
[MIT](../../LICENSE)
