using Projects;

var builder = DistributedApplication.CreateBuilder(args);

bool strictMode = true;

var db = builder.AddSurrealServer("surreal", strictMode: strictMode)
    .WithSurrealist()
    .AddNamespace("ns")
    .AddDatabase("db");

if (strictMode)
{
    db.WithCreationScript(
        $"""
        DEFINE DATABASE IF NOT EXISTS {nameof(db)};
        USE DATABASE {nameof(db)};
        
        DEFINE TABLE todo;
        DEFINE FIELD title ON todo TYPE string;
        DEFINE FIELD dueBy ON todo TYPE datetime;
        DEFINE FIELD isComplete ON todo TYPE bool;
        
        DEFINE TABLE weatherForecast;
        DEFINE FIELD date ON weatherForecast TYPE datetime;
        DEFINE FIELD country ON weatherForecast TYPE string;
        DEFINE FIELD temperatureC ON weatherForecast TYPE number;
        DEFINE FIELD summary ON weatherForecast TYPE string;
        """
    );
}

builder.AddProject<CommunityToolkit_Aspire_Hosting_SurrealDb_ApiService>("apiservice")
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();
