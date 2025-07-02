## Coding Style

-   Use spaces for indentation with four-spaces per level, unless it is a csproj file, then use two-spaces per level.
-   For integrations use the following namespace rules:
    -   If the integration is a hosting integration (starts with CommunityToolkit.Aspire.Hosting), extension methods should be placed in `Aspire.Hosting`.
    -   If the integration is a client integration (starts with CommunityToolkit.Aspire), extension methods should be placed in `Microsoft.Extensions.Hosting`.
    -   Use file-scoped namespaces.
-   All public members require doc comments.
-   Prefer type declarations over `var` when the type isn't obvious.
-   Use the C# Collection Initializer syntax, `List<T> items = []` (where `List<T>` could be any collection type), rather than `new()`.
-   Use `is not null` or `is null` over `!= null` and `== null`.

## Sample hosting integration

```csharp
namespace Aspire.Hosting;

public static class SomeProgramExtensions
{
    public static IResourceBuilder<SomeProgramResource> AddSomeProgram(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name)
    {
        var resource = new SomeProgramResource(name);
        return builder.AddResource(resource);
    }
}
```
