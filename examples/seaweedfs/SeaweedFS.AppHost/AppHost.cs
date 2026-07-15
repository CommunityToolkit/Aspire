IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// =========================================================================
// 🌊 SEAWEEDFS CONFIGURATION EXAMPLES
// =========================================================================
// The integration provides flexible options to configure APIs, ports, and security.
//
// 1. STANDARD S3 SETUP (Default ports dynamically mapped by Aspire)
//    builder.AddSeaweedFS("seaweedfs").WithS3().WithDataVolume();
//
// 2. NATIVE FILER SETUP (No S3 overhead)
//    builder.AddSeaweedFS("seaweedfs").WithFiler().WithDataVolume();
//
// 3. ADVANCED SETUP (Custom ports, credentials, and persistence)
//    var accessKey = builder.AddParameter("s3-access-key", "my-custom-admin");
//    var secretKey = builder.AddParameter("s3-secret-key", "my-super-secret", secret: true);
//
//    builder.AddSeaweedFS("seaweedfs")
//           .WithHostPort(9333)         // Locks the Master API host port
//           .WithS3(s3Port: 8333)       // Locks the S3 Gateway host port
//           .WithFiler(filerPort: 8888) // Locks the Filer API host port
//           .WithAccessKey(accessKey)   // Applies custom S3 Access Key
//           .WithSecretKey(secretKey)   // Applies custom S3 Secret Key
//           .WithDataVolume("my-seaweed-data"); // Uses a named docker volume
// =========================================================================

IResourceBuilder<SeaweedFSContainerResource> seaweedfs = builder.AddSeaweedFS("seaweedfs")
                       .WithS3();      // Use .WithS3() for AWS compatibility or .WithFiler() for native API
                                       //.WithDataVolume(); // Uncomment to enable persistent storage across restarts

// Adds the API and injects the SeaweedFS cluster connection string
builder.AddProject<Projects.SeaweedFS_ApiService>("apiservice")
                        .WithReference(seaweedfs)
                        .WaitFor(seaweedfs)
                        .WithHttpHealthCheck("/health");

builder.Build().Run();