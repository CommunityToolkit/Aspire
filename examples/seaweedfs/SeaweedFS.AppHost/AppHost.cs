using CommunityToolkit.Aspire.Hosting.SeaweedFS;

var builder = DistributedApplication.CreateBuilder(args);

// SeaweedFS Configuration Options:
// 1. WithS3(): Enables the S3-compatible API. BEST FOR: Applications requiring AWS S3-compatible 
//    storage (implicitly enables the Filer API for metadata management).
//
// 2. WithFiler(): Enables only the native SeaweedFS Filer API. BEST FOR: High-performance, 
//    native file system operations without S3 protocol overhead.
//
// 3. WithDataVolume(): Ensures persistence by mapping a Docker volume to the container. 
//    RECOMMENDED for local development if you need to keep data across AppHost restarts.
var seaweedfs = builder.AddSeaweedFS("seaweedfs")
                       .WithS3();      // Use .WithS3() for AWS compatibility or .WithFiler() for native API
                                       //.WithDataVolume(); // Uncomment to enable persistent storage across restarts

// Adds the API and injects the SeaweedFS cluster connection string
builder.AddProject<Projects.SeaweedFS_ApiService>("apiservice")
                        .WithReference(seaweedfs)
                        .WaitFor(seaweedfs)
                        .WithHttpHealthCheck("/health");

builder.Build().Run();