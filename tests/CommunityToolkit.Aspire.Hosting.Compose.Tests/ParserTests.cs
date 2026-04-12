using CommunityToolkit.Aspire.Hosting.Compose.Parsing;
using CommunityToolkit.Aspire.Hosting.Compose.Parsing.Contracts;

namespace CommunityToolkit.Aspire.Hosting.Compose.Tests;

public class ParserTests
{
    [Fact]
    public void Parse_BasicComposeFile_ReturnsServices()
    {
        string composePath = GetTestFilePath("basic.yml");
        ComposeFile composeFile = ComposeParser.Parse(composePath);

        Assert.NotNull(composeFile);
        Assert.Equal(2, composeFile.Services.Count);
        Assert.Contains("postgres", composeFile.Services.Keys);
        Assert.Contains("redis", composeFile.Services.Keys);
    }

    [Fact]
    public void Parse_BasicComposeFile_ParsesImage()
    {
        string composePath = GetTestFilePath("basic.yml");
        ComposeFile composeFile = ComposeParser.Parse(composePath);

        Assert.Equal("postgres:16", composeFile.Services["postgres"].Image);
        Assert.Equal("redis:7-alpine", composeFile.Services["redis"].Image);
    }

    [Fact]
    public void Parse_BasicComposeFile_ParsesPorts()
    {
        string composePath = GetTestFilePath("basic.yml");
        ComposeFile composeFile = ComposeParser.Parse(composePath);

        Assert.NotNull(composeFile.Services["postgres"].Ports);
        Assert.Single(composeFile.Services["postgres"].Ports!);
        Assert.Equal("5432:5432", composeFile.Services["postgres"].Ports![0]);
    }

    [Fact]
    public void Parse_BasicComposeFile_ParsesEnvironment()
    {
        string composePath = GetTestFilePath("basic.yml");
        ComposeFile composeFile = ComposeParser.Parse(composePath);

        Assert.NotNull(composeFile.Services["postgres"].Environment);
    }

    [Fact]
    public void Parse_BasicComposeFile_ParsesVolumes()
    {
        string composePath = GetTestFilePath("basic.yml");
        ComposeFile composeFile = ComposeParser.Parse(composePath);

        Assert.NotNull(composeFile.Services["postgres"].Volumes);
        Assert.Single(composeFile.Services["postgres"].Volumes!);
        Assert.Equal("pgdata:/var/lib/postgresql/data", composeFile.Services["postgres"].Volumes![0]);
    }

    [Fact]
    public void Parse_BasicComposeFile_ParsesDependsOn()
    {
        string composePath = GetTestFilePath("basic.yml");
        ComposeFile composeFile = ComposeParser.Parse(composePath);

        Assert.NotNull(composeFile.Services["redis"].DependsOn);
    }

    [Fact]
    public void Parse_BasicComposeFile_ParsesHealthcheck()
    {
        string composePath = GetTestFilePath("basic.yml");
        ComposeFile composeFile = ComposeParser.Parse(composePath);

        Assert.NotNull(composeFile.Services["postgres"].Healthcheck);
        Assert.Equal(5, composeFile.Services["postgres"].Healthcheck!.Retries);
    }

    [Fact]
    public void Parse_BasicComposeFile_ParsesNamedVolumes()
    {
        string composePath = GetTestFilePath("basic.yml");
        ComposeFile composeFile = ComposeParser.Parse(composePath);

        Assert.NotNull(composeFile.Volumes);
        Assert.Contains("pgdata", composeFile.Volumes.Keys);
    }

    [Fact]
    public void Parse_MissingFile_ThrowsFileNotFoundException() => Assert.Throws<FileNotFoundException>(() => ComposeParser.Parse("nonexistent-compose.yml"));

    [Fact]
    public void ParseYaml_InvalidYaml_ThrowsComposeParseException() => Assert.Throws<ComposeParseException>(() => ComposeParser.ParseYaml("{{invalid yaml!!"));

    [Fact]
    public void ParseYaml_EmptyServices_ThrowsComposeParseException() => Assert.Throws<ComposeParseException>(() => ComposeParser.ParseYaml("services:"));

    [Fact]
    public void Parse_DependsOnConditions_ParsesConditions()
    {
        string composePath = GetTestFilePath("depends-on-conditions.yml");
        ComposeFile composeFile = ComposeParser.Parse(composePath);

        Assert.Equal(3, composeFile.Services.Count);
        Assert.NotNull(composeFile.Services["api"].DependsOn);
    }

    [Fact]
    public void Parse_EnvironmentList_ParsesList()
    {
        string composePath = GetTestFilePath("environment-list.yml");
        ComposeFile composeFile = ComposeParser.Parse(composePath);

        Assert.NotNull(composeFile.Services["app"].Environment);
    }

    private static string GetTestFilePath(string fileName) => Path.Combine(AppContext.BaseDirectory, "composes", fileName);
}
