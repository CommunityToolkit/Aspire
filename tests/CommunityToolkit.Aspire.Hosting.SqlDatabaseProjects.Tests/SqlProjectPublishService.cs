using Aspire.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.Tests;

public class SqlProjectPublisherServiceTests
{
    //[Fact]
    public async Task PublishesDacpacToTargetDatabase()
    {
        // Arrange
        var dacDeployerMock = Substitute.For<IDacpacDeployer>();
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Services.AddSingleton(dacDeployerMock);
        
        var targetDatabase = appBuilder.AddSqlServer("sql")
                                       .WithEndpoint("tcp", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 1433)) // Simulated endpoint
                                       .AddDatabase("test");
        var project = appBuilder.AddSqlProject<TestProject>("MySqlProject")
                  .PublishTo(targetDatabase);
        
        // Act
        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var service = Assert.Single(app.Services.GetServices<SqlProjectPublishService>());
        await service.PublishSqlProject(project.Resource, targetDatabase.Resource, CancellationToken.None);
        
        // Assert
        var expectedPath = Path.GetFullPath(Path.Combine(appBuilder.AppHostDirectory, "../../../../TestProject/bin/Debug/netstandard2.0/TestProject.dacpac"));
        dacDeployerMock.Received().Deploy(Arg.Is(expectedPath), Arg.Any<string>(), Arg.Is("test"), Arg.Any<ILogger>(), Arg.Any<CancellationToken>());
    }
}
