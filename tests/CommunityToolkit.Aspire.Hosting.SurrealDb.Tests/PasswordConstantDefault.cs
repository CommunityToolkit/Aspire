using Aspire.Hosting.Publishing;

namespace CommunityToolkit.Aspire.Hosting.SurrealDb.Tests;

internal sealed class PasswordConstantDefault : ParameterDefault
{
    public override void WriteToManifest(ManifestPublishingContext context)
    {
    }

    public override string GetDefaultValue()
    {
        return "password";
    }
}