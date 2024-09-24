var builder = DistributedApplication.CreateBuilder(args);

var apiapp = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Java_ApiApp>("apiapp");

var containerapp = builder.AddSpringApp("containerapp",
                           new JavaAppContainerResourceOptions()
                           {
                               ContainerImageName = "aliencube/aspire-spring-maven-sample",
                               OtelAgentPath = "/agents"
                           });
var executableapp = builder.AddSpringApp("executableapp",
                           workingDirectory: "../Aspire.CommunityToolkit.Hosting.Java.Spring.Maven",
                           new JavaAppExecutableResourceOptions()
                           {
                               ApplicationName = "target/spring-maven-0.0.1-SNAPSHOT.jar",
                               Port = 8085,
                               OtelAgentPath = "../../../agents",
                           })
                           .PublishAsDockerFile(
                           [
                               new DockerBuildArg("JAR_NAME", "spring-maven-0.0.1-SNAPSHOT.jar"),
                               new DockerBuildArg("AGENT_PATH", "/agents"),
                               new DockerBuildArg("SERVER_PORT", "8085"),
                           ]);

var webapp = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Java_WebApp>("webapp")
                    .WithExternalHttpEndpoints()
                    .WithReference(containerapp)
                    .WithReference(executableapp)
                    .WithReference(apiapp);

builder.Build().Run();
