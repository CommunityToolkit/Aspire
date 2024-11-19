using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Rust.Tests;

public class RustAppPublicApiTests
{
    [Fact]
    public void AddRustAppShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;
        const string name = "rust-app";
        const string workingDirectory = "rust_app";
        var action = () => builder.AddRustApp(name, workingDirectory);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddRustAppShouldThrowWhenNameIsNull()
    {
        IDistributedApplicationBuilder builder = new DistributedApplicationBuilder([]);
        const string name = null!;
        const string workingDirectory = "rust_app";

        var action = () => builder.AddRustApp(name!, workingDirectory);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void AddRustAppShouldThrowWorkingDirectoryIsNull()
    {
        IDistributedApplicationBuilder builder = new DistributedApplicationBuilder([]);
        const string name = "rust-app";
        const string workingDirectory = null!;

        var action = () => builder.AddRustApp(name, workingDirectory!);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(workingDirectory), exception.ParamName);
    }
}
