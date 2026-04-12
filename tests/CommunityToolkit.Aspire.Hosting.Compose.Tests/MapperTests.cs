using CommunityToolkit.Aspire.Hosting.Compose.Mapping;
namespace CommunityToolkit.Aspire.Hosting.Compose.Tests;

public class MapperTests
{
    [Fact]
    public void ParseEnvironment_DictionaryFormat_ReturnsPairs()
    {
        Dictionary<object, object> env = new()
        {
            { "KEY1", "value1" },
            { "KEY2", "value2" }
        };

        Dictionary<string, string> result = EnvironmentMapper.Parse(env);

        Assert.Equal(2, result.Count);
        Assert.Equal("value1", result["KEY1"]);
        Assert.Equal("value2", result["KEY2"]);
    }

    [Fact]
    public void ParseEnvironment_ListFormat_ReturnsPairs()
    {
        List<object> env =
        [
            "NODE_ENV=production",
            "PORT=3000",
            "DEBUG="
        ];

        Dictionary<string, string> result = EnvironmentMapper.Parse(env);

        Assert.Equal(3, result.Count);
        Assert.Equal("production", result["NODE_ENV"]);
        Assert.Equal("3000", result["PORT"]);
        Assert.Equal("", result["DEBUG"]);
    }

    [Fact]
    public void ParseDependsOn_ListFormat_ReturnsServiceStarted()
    {
        List<object> deps = ["postgres", "redis"];

        Dictionary<string, string> result = DependsOnMapper.Parse(deps);

        Assert.Equal(2, result.Count);
        Assert.Equal("service_started", result["postgres"]);
        Assert.Equal("service_started", result["redis"]);
    }

    [Fact]
    public void ParseDependsOn_DictionaryFormat_ReturnsConditions()
    {
        Dictionary<object, object> deps = new()
        {
            {
                "db", new Dictionary<object, object>
                {
                    { "condition", "service_healthy" }
                }
            },
            {
                "migrations", new Dictionary<object, object>
                {
                    { "condition", "service_completed_successfully" }
                }
            }
        };

        Dictionary<string, string> result = DependsOnMapper.Parse(deps);

        Assert.Equal(2, result.Count);
        Assert.Equal("service_healthy", result["db"]);
        Assert.Equal("service_completed_successfully", result["migrations"]);
    }

    [Fact]
    public void ParseStringOrList_String_SplitsBySpaces()
    {
        string[] result = ServiceToResourceMapper.ParseStringOrList("arg1 arg2 arg3");

        Assert.Equal(3, result.Length);
        Assert.Equal("arg1", result[0]);
        Assert.Equal("arg2", result[1]);
        Assert.Equal("arg3", result[2]);
    }

    [Fact]
    public void ParseStringOrList_List_ReturnsItems()
    {
        List<object> list = ["arg1", "arg2", "arg3"];

        string[] result = ServiceToResourceMapper.ParseStringOrList(list);

        Assert.Equal(3, result.Length);
        Assert.Equal("arg1", result[0]);
    }
}
