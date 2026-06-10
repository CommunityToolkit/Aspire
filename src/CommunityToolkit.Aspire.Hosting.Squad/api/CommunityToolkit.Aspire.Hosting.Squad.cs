namespace Aspire.Hosting.ApplicationModel
{
    public sealed partial class SquadResource : Aspire.Hosting.ApplicationModel.Resource, Aspire.Hosting.ApplicationModel.IResourceWithConnectionString
    {
        public SquadResource(string name, string teamRoot) : base(default!) { }
        public System.Collections.Generic.IReadOnlyList<string> Agents { get { throw null; } }
        public Aspire.Hosting.ApplicationModel.ReferenceExpression ConnectionStringExpression { get { throw null; } }
        public string TeamRoot { get { throw null; } }
    }
}

namespace Aspire.Hosting
{
    public static partial class SquadBuilderExtensions
    {
        public static Aspire.Hosting.ApplicationModel.IResourceBuilder<Aspire.Hosting.ApplicationModel.SquadResource> AddSquad(this Aspire.Hosting.IDistributedApplicationBuilder builder, string name, string teamRoot) { throw null; }
    }
}
