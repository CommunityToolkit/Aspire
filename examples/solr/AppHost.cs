var builder = DistributedApplication.CreateBuilder(args);

// Add Solr resource with default core name "solr"
var solr = builder.AddSolr("solr");

// Add Solr resource with custom port and core name
var solrWithCustomPort = builder.AddSolr("solr-custom", port: 8984, coreName: "mycore");

//Add Solr resource with a config set annotation
var solrWithConfigset = builder.AddSolr("solr-configset")
    .WithConfigset("myconfigset", "/path/to/configset");

// Reference the Solr resources in a project (example)
// var exampleProject = builder.AddProject<Projects.ExampleProject>()
//                             .WithReference(solr);
builder.Build().Run();
