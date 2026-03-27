var builder = DistributedApplication.CreateBuilder(args);

var apiapp = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Java_ApiApp>("apiapp");

var containerapp = builder.AddJavaContainerApp("containerapp", image: "docker.io/aliencube/aspire-spring-maven-sample")
                          .WithOtelAgent("/agents/opentelemetry-javaagent.jar")
                          .WithHttpEndpoint(targetPort: 8080, env: "SERVER_PORT");

var executableapp = builder.AddJavaApp("executableapp",
                           workingDirectory: "../CommunityToolkit.Aspire.Hosting.Java.Spring.Maven")
                           .WithMavenGoal("spring-boot:run")
                           .WithOtelAgent("../../../agents/opentelemetry-javaagent.jar")
                           .WithHttpEndpoint(targetPort: 8080, env: "SERVER_PORT")
                           .WithHttpHealthCheck("/health");

var webapp = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Java_WebApp>("webapp")
                    .WithExternalHttpEndpoints()
                    .WithReference(containerapp)
                    .WithReference(executableapp)
                    .WithReference(apiapp);

builder.Build().Run();
