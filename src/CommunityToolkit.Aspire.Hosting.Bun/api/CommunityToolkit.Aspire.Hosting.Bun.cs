//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
namespace Aspire.Hosting
{
    public static partial class BunAppExtensions
    {
        public static ApplicationModel.IResourceBuilder<ApplicationModel.BunAppResource> AddBunApp(this IDistributedApplicationBuilder builder, string name, string? workingDirectory = null, string entryPoint = "index.ts", bool watch = false) { throw null; }

        public static ApplicationModel.IResourceBuilder<ApplicationModel.BunAppResource> WithBunPackageInstallation(this ApplicationModel.IResourceBuilder<ApplicationModel.BunAppResource> resource) { throw null; }
    }
}

namespace Aspire.Hosting.ApplicationModel
{
    public partial class BunAppResource : ExecutableResource
    {
        public BunAppResource(string name, string workingDirectory) : base(default!, default!, default!) { }
    }
}