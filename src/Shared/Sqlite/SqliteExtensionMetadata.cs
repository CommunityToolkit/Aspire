namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Represents metadata for an extension to be loaded into a database.
/// </summary>
/// <param name="Extension">The name of the extension binary, eg: vec0</param>
/// <param name="PackageName">The name of the NuGet package. Only required if <paramref name="IsNuGetPackage"/> is <see langword="true" />.</param>
/// <param name="IsNuGetPackage">Indicates if the extension will be loaded from a NuGet package.</param>
/// <param name="ExtensionFolder">The folder for the extension. Only required if <paramref name="IsNuGetPackage"/> is <see langword="false" />.</param>
public record SqliteExtensionMetadata(string Extension, string? PackageName, bool IsNuGetPackage, string? ExtensionFolder);