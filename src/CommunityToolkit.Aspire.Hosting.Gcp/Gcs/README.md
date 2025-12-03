# CommunityToolkit.Aspire.Hosting.Gcp.Gcs
Provides extension methods and resource definitions for a .NET Aspire AppHost to configure Gcs and buckets.

## Usage Example
In the AppHost.cs file of your AppHost project, register the Gcs Server and then add bucket definitions for each bucket you want.

```c#
var projectId = builder.AddParameter("project-id");
// Optional initDataDirectory
var gcs = builder.AddGcs("gcs", projectId, certPath: "path/to/ssl/cert", initDataDirectory: "path/to/preinitialize/content/directory");

//optional custom templating of bucket name
var bucket = gcs.AddBucket("bucket-name", ReferenceExpression.Create($"{projectId}-bucket-name"));

//usage - supports WaitFor and environment passing for buckets
var service = builder.AddContainer(...)
    .WithEnvironemnt("GCP_PROJECT_ID", gcs.Resource.ProjectId)
    .WithEnvironment("GCS_BUCKET_NAME", bucket)
    .WaitFor(bucket); // will wait for bucket creation

if (builder.ExecutionContext.IsRunMode)
{
    service
        .WithEnvironment("GCS_ENDPOINT", gcs) // will automatically pass the correct endpoint
        .WithEnvironment("GCP_APPLICATION_CREDENTIALS", "path/to/ssl/cert")
}
```
