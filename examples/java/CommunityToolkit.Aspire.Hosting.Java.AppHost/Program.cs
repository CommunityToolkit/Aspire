var builder = DistributedApplication.CreateBuilder(args);

var apiapp = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Java_ApiApp>("apiapp");
var containerapp = builder.AddSpringApp("containerapp",
                                     new JavaAppContainerResourceOptions()
                                     {
                                         ContainerImageName = "aliencube/aspire-spring-maven-sample",
                                         OtelAgentPath = "/agents"
                                     });

IResourceBuilder<JavaAppExecutableResource>? executableapp = default;
if (builder.ExecutionContext.IsPublishMode == false)
{
    executableapp = builder.AddSpringApp("executableapp",
                                         workingDirectory: "../CommunityToolkit.Aspire.Hosting.Java.Spring.Maven",
                                         new JavaAppExecutableResourceOptions()
                                         {
                                             ApplicationName = "target/spring-maven-0.0.1-SNAPSHOT.jar",
                                             Port = 8085,
                                             OtelAgentPath = "../../../agents",
                                         });
}

var webapp = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Java_WebApp>("webapp")
                    .WithExternalHttpEndpoints()
                    .WithReference(containerapp)
                    .WithReference(apiapp);

if (builder.ExecutionContext.IsPublishMode == false)
{
#pragma warning disable CS8604 // Possible null reference argument.
    webapp.WithReference(executableapp)
          .WithEnvironment("USE_EXECUTABLE", "true");
#pragma warning restore CS8604 // Possible null reference argument.
}

builder.Build().Run();
