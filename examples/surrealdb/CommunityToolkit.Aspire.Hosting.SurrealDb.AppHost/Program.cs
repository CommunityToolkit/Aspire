using Projects;

var builder = DistributedApplication.CreateBuilder(args);

bool strictMode = false;

var db = builder.AddSurrealServer("surreal")
    .WithSurrealist()
    .AddNamespace("ns")
    .AddDatabase("db");

#pragma warning disable CTASPIRE002 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
string strictSuffix = strictMode ? " STRICT" : string.Empty;
db.WithCreationScript(
    $"""
    DEFINE DATABASE IF NOT EXISTS {nameof(db)}{strictSuffix};
    USE DATABASE {nameof(db)};
    
    DEFINE TABLE todo;
    DEFINE FIELD title ON todo TYPE string;
    DEFINE FIELD due_by ON todo TYPE datetime;
    DEFINE FIELD is_complete ON todo TYPE bool;
    
    DEFINE TABLE weatherForecast;
    DEFINE FIELD date ON weatherForecast TYPE datetime;
    DEFINE FIELD country ON weatherForecast TYPE string;
    DEFINE FIELD temperature_c ON weatherForecast TYPE number;
    DEFINE FIELD temperature_f ON weatherForecast TYPE number;
    DEFINE FIELD summary ON weatherForecast TYPE string;
    """
);
#pragma warning restore CTASPIRE002 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.


builder.AddProject<CommunityToolkit_Aspire_Hosting_SurrealDb_ApiService>("apiservice")
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();
