namespace Aspire.Hosting
{
    public static partial class ChromaBuilderExtensions
    {
        public static ApplicationModel.IResourceBuilder<ApplicationModel.ChromaResource> AddChroma(this IDistributedApplicationBuilder builder, string name, int? port = null) { throw null; }

        public static ApplicationModel.IResourceBuilder<ApplicationModel.ChromaResource> WithDataBindMount(this ApplicationModel.IResourceBuilder<ApplicationModel.ChromaResource> builder, string source) { throw null; }

        public static ApplicationModel.IResourceBuilder<ApplicationModel.ChromaResource> WithDataVolume(this ApplicationModel.IResourceBuilder<ApplicationModel.ChromaResource> builder, string? name = null) { throw null; }
    }
}

namespace Aspire.Hosting.ApplicationModel
{
    public partial class ChromaResource : ContainerResource, IResourceWithConnectionString, IResource, IManifestExpressionProvider, IValueProvider, IValueWithReferences
    {
        public ChromaResource(string name) : base(default!, default) { }

        public ReferenceExpression ConnectionStringExpression { get { throw null; } }

        public EndpointReferenceExpression Host { get { throw null; } }

        public EndpointReferenceExpression Port { get { throw null; } }

        public EndpointReference PrimaryEndpoint { get { throw null; } }

        public ReferenceExpression UriExpression { get { throw null; } }

        System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties() { throw null; }
    }
}
