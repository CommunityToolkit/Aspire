// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.Utils;

namespace CommunityToolkit.Aspire.Hosting.SurrealDb.Tests;

public class SurrealDbPublicApiTests
{
    [Fact]
    public void AddSurrealServerContainerShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;
        const string name = "surreal";

        var action = () => builder.AddSurrealServer(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddSurrealServerContainerShouldThrowWhenNameIsNull()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        string name = null!;

        var action = () => builder.AddSurrealServer(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void AddDatabaseShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<SurrealDbNamespaceResource> builder = null!;
        const string name = "surreal";

        var action = () => builder.AddDatabase(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddDatabaseShouldThrowWhenNameIsNull()
    {
        var builder = TestDistributedApplicationBuilder.Create()
            .AddSurrealServer("surreal")
            .AddNamespace("ns");
        string name = null!;

        var action = () => builder.AddDatabase(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void AddDatabaseShouldThrowWhenNameIsEmpty()
    {
        var builder = TestDistributedApplicationBuilder.Create()
            .AddSurrealServer("surreal")
            .AddNamespace("ns");
        string name = "";

        var action = () => builder.AddDatabase(name);

        var exception = Assert.Throws<ArgumentException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void WithDataVolumeShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<SurrealDbServerResource> builder = null!;

        var action = () => builder.WithDataVolume();

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithDataShouldThrowWhenBuilderIsNull(bool useVolume)
    {
        IResourceBuilder<SurrealDbServerResource> builder = null!;
    
        Func<IResourceBuilder<SurrealDbServerResource>>? action = null;
    
        if (useVolume)
        {
            action = () => builder.WithDataVolume();
        }
        else
        {
            const string source = "/data";
    
            action = () => builder.WithDataBindMount(source);
        }
    
        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }
    
    [Fact]
    public void WithDataBindMountShouldThrowWhenSourceIsNull()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        var resourceBuilder = builder.AddSurrealServer("surreal");
    
        string source = null!;
    
        var action = () => resourceBuilder.WithDataBindMount(source);
    
        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(source), exception.ParamName);
    }
    
    
    [Fact]
    public void WithInitFilesShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<SurrealDbServerResource> builder = null!;
        const string source = "/surrealdb/init.sql";

        var action = () => builder.WithInitFiles(source);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WithInitFilesShouldThrowWhenSourceIsNullOrEmpty(bool isNull)
    {
        var builder = TestDistributedApplicationBuilder.Create()
            .AddSurrealServer("surreal");
        var source = isNull ? null! : string.Empty;

        var action = () => builder.WithInitFiles(source);

        var exception = isNull
            ? Assert.Throws<ArgumentNullException>(action)
            : Assert.Throws<ArgumentException>(action);
        Assert.Equal(nameof(source), exception.ParamName);
    }

    [Fact]
    public void WithSurrealistShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<SurrealDbServerResource> builder = null!;

        var action = () => builder.WithSurrealist();

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void CtorSurrealServerServerResourceShouldThrowWhenNameIsNull()
    {
        var distributedApplicationBuilder = TestDistributedApplicationBuilder.Create();
        string name = null!;
        const string key = nameof(key);
        var password = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(distributedApplicationBuilder, key, special: false);

        var action = () => new SurrealDbServerResource(name, null, password);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void CtorSurrealServerDatabaseResourceShouldThrowWhenNameIsNull()
    {
        var distributedApplicationBuilder = TestDistributedApplicationBuilder.Create();

        string name = null!;
        string namespaceName = "ns1";
        string databaseName = "db1";
        var password = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(distributedApplicationBuilder, "password", special: false);
        var parent = new SurrealDbServerResource("surreal", null, password);
        var nsParent = new SurrealDbNamespaceResource("ns", namespaceName, parent);
        var action = () => new SurrealDbDatabaseResource(name, databaseName, nsParent);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void CtorSurrealServerDatabaseResourceShouldThrowWhenNameIsEmpty()
    {
        var distributedApplicationBuilder = TestDistributedApplicationBuilder.Create();

        string name = "";
        string namespaceName = "ns1";
        string databaseName = "db1";
        var password = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(distributedApplicationBuilder, "password", special: false);
        var parent = new SurrealDbServerResource("surreal", null, password);
        var nsParent = new SurrealDbNamespaceResource("ns", namespaceName, parent);
        var action = () => new SurrealDbDatabaseResource(name, databaseName, nsParent);

        var exception = Assert.Throws<ArgumentException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void CtorSurrealServerNamespaceResourceShouldThrowWhenNamespaceNameIsNull()
    {
        var distributedApplicationBuilder = TestDistributedApplicationBuilder.Create();

        string namespaceName = null!;
        var password = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(distributedApplicationBuilder, "password", special: false);
        var parent = new SurrealDbServerResource("surreal", null, password);
        var action = () => new SurrealDbNamespaceResource("ns", namespaceName, parent);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(namespaceName), exception.ParamName);
    }

    [Fact]
    public void CtorSurrealServerNamespaceResourceShouldThrowWhenNamespaceNameIsEmpty()
    {
        var distributedApplicationBuilder = TestDistributedApplicationBuilder.Create();

        string namespaceName = "";
        var password = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(distributedApplicationBuilder, "password", special: false);
        var parent = new SurrealDbServerResource("surreal", null, password);
        var action = () => new SurrealDbNamespaceResource("ns", namespaceName, parent);

        var exception = Assert.Throws<ArgumentException>(action);
        Assert.Equal(nameof(namespaceName), exception.ParamName);
    }

    [Fact]
    public void CtorSurrealServerDatabaseResourceShouldThrowWhenDatabaseNameIsNull()
    {
        var distributedApplicationBuilder = TestDistributedApplicationBuilder.Create();

        string name = "surreal";
        string namespaceName = "ns";
        string databaseName = null!;
        var password = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(distributedApplicationBuilder, "password", special: false);
        var parent = new SurrealDbServerResource("surreal", null, password);
        var nsParent = new SurrealDbNamespaceResource("ns", namespaceName, parent);
        var action = () => new SurrealDbDatabaseResource(name, databaseName, nsParent);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(databaseName), exception.ParamName);
    }

    [Fact]
    public void CtorSurrealServerDatabaseResourceShouldThrowWhenDatabaseNameIsEmpty()
    {
        var distributedApplicationBuilder = TestDistributedApplicationBuilder.Create();

        string name = "surreal";
        string namespaceName = "ns";
        string databaseName = null!;
        var password = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(distributedApplicationBuilder, "password", special: false);
        var parent = new SurrealDbServerResource("surreal", null, password);
        var nsParent = new SurrealDbNamespaceResource("ns", namespaceName, parent);
        var action = () => new SurrealDbDatabaseResource(name, databaseName, nsParent);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(databaseName), exception.ParamName);
    }

    [Fact]
    public void CtorSurrealServerDatabaseResourceShouldThrowWhenParentIsNull()
    {
        string name = "surreal";
        string databaseName = "db1";
        SurrealDbNamespaceResource parent = null!;
        var action = () => new SurrealDbDatabaseResource(name, databaseName, parent);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(parent), exception.ParamName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CtorSurrealistContainerResourceShouldThrowWhenNameIsNullOrEmpty(bool isNull)
    {
        var name = isNull ? null! : string.Empty;

        var action = () => new SurrealistContainerResource(name);

        var exception = isNull
            ? Assert.Throws<ArgumentNullException>(action)
            : Assert.Throws<ArgumentException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }
}
