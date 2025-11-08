// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Umami.Tests;

public class UmamiPublicApiTests
{
    [Fact]
    public void AddUmamiShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;
        const string name = "umami";

        var action = () => builder.AddUmami(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddUmamiShouldThrowWhenNameIsNull()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        string name = null!;

        var action = () => builder.AddUmami(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

     [Fact]
     public void WithStorageBackendShouldThrowWhenBuilderIsNull()
     {
         IResourceBuilder<UmamiResource> builder = null!;

         var action = () => builder.WithStorageBackend(null!);

         var exception = Assert.Throws<ArgumentNullException>(action);
         Assert.Equal(nameof(builder), exception.ParamName);
     }

    [Fact]
    public void CtorUmamiResourceShouldThrowWhenNameIsNull()
    {
        var distributedApplicationBuilder = TestDistributedApplicationBuilder.Create();
        string name = null!;
        const string key = nameof(key);
        var secret = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(distributedApplicationBuilder, key, special: false);

        var action = () => new UmamiResource(name, secret);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }
}