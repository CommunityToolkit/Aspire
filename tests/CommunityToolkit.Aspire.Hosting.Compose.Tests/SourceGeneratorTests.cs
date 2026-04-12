using CommunityToolkit.Aspire.Hosting.Compose.Generator;

namespace CommunityToolkit.Aspire.Hosting.Compose.Tests;

/// <summary>
/// Tests for the compose source generator's service name extractor and class emitter.
/// </summary>
public class SourceGeneratorTests
{

    [Fact]
    public void Extract_ModernFormat_FindsServices()
    {
        const string yaml = """
                            services:
                              postgres:
                                image: postgres:16
                              redis:
                                image: redis:7
                              kafka:
                                image: confluentinc/cp-kafka:7.5.0
                            """;

        List<string> names = ComposeServiceNameExtractor.Extract(yaml);

        Assert.Equal(3, names.Count);
        Assert.Contains("postgres", names);
        Assert.Contains("redis", names);
        Assert.Contains("kafka", names);
    }

    [Fact]
    public void Extract_V1Format_FindsTopLevelServices()
    {
        const string yaml = """
                            postgres:
                              image: postgres:14
                              ports:
                                - "5432:5432"
                            redis:
                              image: redis:6
                            """;

        List<string> names = ComposeServiceNameExtractor.Extract(yaml);

        Assert.Equal(2, names.Count);
        Assert.Contains("postgres", names);
        Assert.Contains("redis", names);
    }

    [Fact]
    public void Extract_V2Format_IgnoresVersionField()
    {
        const string yaml = """
                            version: "2.4"
                            services:
                              db:
                                image: postgres:14
                              cache:
                                image: redis:6
                            volumes:
                              data:
                            """;

        List<string> names = ComposeServiceNameExtractor.Extract(yaml);

        Assert.Equal(2, names.Count);
        Assert.Contains("db", names);
        Assert.Contains("cache", names);
    }

    [Fact]
    public void Extract_V3Format_IgnoresVersionField()
    {
        const string yaml = """
                            version: "3.8"
                            services:
                              web:
                                image: nginx:1.25
                              api:
                                image: myapi:latest
                            """;

        List<string> names = ComposeServiceNameExtractor.Extract(yaml);

        Assert.Equal(2, names.Count);
        Assert.Contains("web", names);
        Assert.Contains("api", names);
    }

    [Fact]
    public void Extract_EmptyContent_ReturnsEmpty()
    {
        List<string> names = ComposeServiceNameExtractor.Extract(string.Empty);
        Assert.Empty(names);
    }

    [Fact]
    public void Extract_CommentsIgnored()
    {
        const string yaml = """
                            # This is a comment
                            services:
                              # Another comment
                              postgres:
                                image: postgres:16
                            """;

        List<string> names = ComposeServiceNameExtractor.Extract(yaml);

        Assert.Single(names);
        Assert.Contains("postgres", names);
    }

    [Fact]
    public void Extract_V1WithVolumesSection_ExcludesVolumes()
    {
        const string yaml = """
                            postgres:
                              image: postgres:14
                            volumes:
                              pgdata:
                            networks:
                              mynet:
                            """;

        List<string> names = ComposeServiceNameExtractor.Extract(yaml);

        Assert.Single(names);
        Assert.Contains("postgres", names);
    }


    [Fact]
    public void SanitizeIdentifier_PascalCase()
    {
        Assert.Equal("Infra", ComposeClassEmitter.SanitizeIdentifier("Infra"));
        Assert.Equal("Postgres", ComposeClassEmitter.SanitizeIdentifier("postgres"));
    }

    [Fact]
    public void SanitizeIdentifier_KebabCase_Converts()
    {
        Assert.Equal("MyInfra", ComposeClassEmitter.SanitizeIdentifier("my-infra"));
        Assert.Equal("MyPostgres", ComposeClassEmitter.SanitizeIdentifier("my-postgres"));
    }

    [Fact]
    public void SanitizeIdentifier_SnakeCase_Converts()
    {
        Assert.Equal("MyInfra", ComposeClassEmitter.SanitizeIdentifier("my_infra"));
    }

    [Fact]
    public void SanitizeIdentifier_StartsWithDigit_Prefixed()
    {
        Assert.Equal("_3rdParty", ComposeClassEmitter.SanitizeIdentifier("3rd-party"));
    }

    [Fact]
    public void EmitClass_GeneratesWrapperWithProperties()
    {
        string source = ComposeClassEmitter.EmitClass(
            "Infra",
            ".infra/compose.yml",
            ["postgres", "redis", "kafka"]);

        Assert.Contains("namespace Compose", source);
        Assert.Contains("public sealed class Infra", source);
        Assert.Contains("ComposeReferencePath", source);
        // Typed properties
        Assert.Contains("public global::Aspire.Hosting.ApplicationModel.IResourceBuilder<global::Aspire.Hosting.ApplicationModel.ContainerResource> Postgres => _inner[\"postgres\"];", source);
        Assert.Contains("public global::Aspire.Hosting.ApplicationModel.IResourceBuilder<global::Aspire.Hosting.ApplicationModel.ContainerResource> Redis => _inner[\"redis\"];", source);
        Assert.Contains("public global::Aspire.Hosting.ApplicationModel.IResourceBuilder<global::Aspire.Hosting.ApplicationModel.ContainerResource> Kafka => _inner[\"kafka\"];", source);
        // Constructor
        Assert.Contains("ComposeResourceCollection inner", source);
        // String indexer for fallback
        Assert.Contains("this[string serviceName]", source);
    }

    [Fact]
    public void EmitClass_EmptyServices_GeneratesClassWithIndexerOnly()
    {
        string source = ComposeClassEmitter.EmitClass("Empty", "empty.yml", []);

        Assert.Contains("public sealed class Empty", source);
        Assert.Contains("this[string serviceName]", source);
        Assert.DoesNotContain("=> _inner[\"", source);
    }

    [Fact]
    public void SanitizeIdentifier_EmptyString_ReturnsUnknown()
    {
        Assert.Equal("Unknown", ComposeClassEmitter.SanitizeIdentifier(string.Empty));
    }

    [Fact]
    public void SanitizeIdentifier_AllDelimiters_ReturnsUnknown()
    {
        Assert.Equal("Unknown", ComposeClassEmitter.SanitizeIdentifier("---"));
    }

    [Fact]
    public void SanitizeIdentifier_DotSeparator_Converts()
    {
        Assert.Equal("MyInfra", ComposeClassEmitter.SanitizeIdentifier("my.infra"));
    }

    [Fact]
    public void SanitizeIdentifier_SpaceSeparator_Converts()
    {
        Assert.Equal("MyInfra", ComposeClassEmitter.SanitizeIdentifier("my infra"));
    }

    [Fact]
    public void Extract_WhitespaceOnly_ReturnsEmpty()
    {
        List<string> names = ComposeServiceNameExtractor.Extract("   \n\t  ");
        Assert.Empty(names);
    }

    [Fact]
    public void Extract_QuotedServiceNames_StripsQuotes()
    {
        const string yaml = """
                            services:
                              "quoted-service":
                                image: test:1
                            """;

        List<string> names = ComposeServiceNameExtractor.Extract(yaml);

        Assert.Single(names);
        Assert.Contains("quoted-service", names);
    }

    [Fact]
    public void Extract_TabIndentation_HandlesCorrectly()
    {
        string yaml = "services:\n\tpostgres:\n\t\timage: postgres:16";

        List<string> names = ComposeServiceNameExtractor.Extract(yaml);

        Assert.Single(names);
        Assert.Contains("postgres", names);
    }
}
