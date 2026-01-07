# CommunityToolkit.Aspire.Hosting.Elasticsearch.Extensions library

This integration contains extensions for the [Elasticsearch hosting package](https://nuget.org/packages/Aspire.Hosting.Elasticsearch) for .NET Aspire.

The integration provides support for running [Elasticvue](https://github.com/cars10/elasticvue) to interact with the Elasticsearch database.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Elasticsearch.Extensions
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define an Elasticsearch resource, then call `WithElasticvue`:

```csharp
var elasticsearch = builder.AddElasticsearch("elasticsearch")
    .WithElasticvue();
```

## Feedback & contributing

<https://github.com/CommunityToolkit/Aspire>
