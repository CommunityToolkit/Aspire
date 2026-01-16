var builder = DistributedApplication.CreateBuilder(args);

var golang = builder.AddGolangApp("golang", "../gin-api")
    .WithHttpEndpoint(env: "PORT")
    .WithHttpHealthCheck("/health");

// Example: Copy static frontend files into the Golang container when publishing
// This demonstrates the IContainerFilesDestinationResource support
// Uncomment the following line to copy files from a static directory:
// golang.WithContainerFiles("/app/static", "../static-frontend");

// Or, when using a frontend build resource (e.g., Vite, React, etc.):
// var frontend = builder.AddViteApp("frontend", "../frontend");
// golang.WithContainerFiles("/app/static", frontend.Resource);

builder.Build().Run();