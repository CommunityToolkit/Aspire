namespace Aspire.Hosting.ApplicationModel;

internal static class SqliteContainerImageTags
{
    public const string SqliteWebImage = "coleifer/sqlite-web";
    public const string SqliteWebTag = "latest"; // we have to use `latest` as the image doesn't publish any other image tags.
    public const string SqliteWebRegistry = "ghcr.io";
}