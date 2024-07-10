using Aspire.Contribs.Hosting.Java;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddSpringApp("containerapp",
                     new JavaAppContainerResourceOptions()
                     {
                         ContainerImageName = "aspire-contribs/spring-maven-sample",
                         OtelAgentPath = "/agents"
                     });

builder.AddSpringApp("executableapp",
                     workingDirectory: "../../src/Aspire.Contribs.Spring.Maven",
                     new JavaAppExecutableResourceOptions()
                     {
                         ApplicationName = "target/spring-maven-0.0.1-SNAPSHOT.jar",
                         Port = 8085,
                         OtelAgentPath = "../../agents",
                     });

builder.Build().Run();
