var builder = DistributedApplication.CreateBuilder(args);

#pragma warning disable ASPIREDOTNETPROJECT001
var apiapp = builder.AddDotnetProject("apiapp", new Projects.CommunityToolkit_Aspire_Hosting_Java_ApiApp().ProjectPath);
#pragma warning restore ASPIREDOTNETPROJECT001

var containerapp = builder.AddJavaContainerApp("containerapp", image: "docker.io/aliencube/aspire-spring-maven-sample")
                          .WithOtelAgent("/agents/opentelemetry-javaagent.jar")
                          .WithHttpEndpoint(targetPort: 8080, env: "SERVER_PORT")
                          .WithHttpHealthCheck("/");

var executableapp = builder.AddJavaApp("executableapp",
                           workingDirectory: "../CommunityToolkit.Aspire.Hosting.Java.Spring.Maven")
                           .WithMavenGoal("spring-boot:run")
                           .WithOtelAgent("../../../agents/opentelemetry-javaagent.jar")
                           .WithHttpEndpoint(targetPort: 8080, env: "SERVER_PORT")
                           .WithHttpHealthCheck("/health");

#pragma warning disable ASPIREDOTNETPROJECT001
var webapp = builder.AddDotnetProject("webapp", new Projects.CommunityToolkit_Aspire_Hosting_Java_WebApp().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
                    .WithExternalHttpEndpoints()
                    .WithReference(containerapp)
                    .WithReference(executableapp)
                    .WithReference(apiapp);

builder.Build().Run();
