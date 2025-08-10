var builder = DistributedApplication.CreateBuilder(args);

// Add Solr resource
var solr = builder.AddSolr("solr");

// Set Port Number (using the port parameter in AddSolr)
var solrWithCustomPort = builder.AddSolr("solr-custom", port: 8984);

// Add a Solr Core
var solrCore = solr.AddSolrCore("solrcore");

// Reference the Solr Core in a project (example)
// var exampleProject = builder.AddProject<Projects.ExampleProject>()
//                             .WithReference(solrCore);

builder.Build().Run();
