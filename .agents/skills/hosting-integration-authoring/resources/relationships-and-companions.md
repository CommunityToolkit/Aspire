# Relationships, dependencies, and companions

Aspire relationships are semantic. Pick the relationship that matches the behavior.

## Parent-child resources

Use parent-child structure for domain subresources owned by a parent service.

DO:

- Implement `IResourceWithParent<TParent>` on child resource types.
- Expose child creation on the parent builder: `server.AddDatabase("db")`, `storage.AddBlobContainer("images")`.
- Use globally unique Aspire resource names and separate physical names.
- Keep child auth and endpoint logic delegated to the parent.

DON'T:

- Don't add top-level `builder.AddDatabase(...)` for a database that belongs to one server.
- Don't make the child re-implement parent connection, auth, emulator, or Azure branching.

## Resource references

Use `WithReference` when a consumer needs connection info, service discovery, or integration-provided environment variables.

For overlay/configuration objects, define clear projection APIs such as `AsClient()` or `WithReference<TOverlay>()` so users understand what gets wired.

## Parent and custom relationships

Use `WithParentRelationship` when a companion or child resource is visually/semantically attached to a parent in the app model.

Use `WithRelationship(parent, "Label")` for display or custom semantic relationships that are not necessarily ownership.

## Setup siblings

Run-mode setup resources prepare an app before it starts.

DO:

- Create setup siblings only in run mode.
- Name setup siblings predictably, for example `{resource}-mod-tidy`.
- Mark setup siblings `.ExcludeFromManifest()`.
- Make the main resource wait with `WaitForCompletion`.
- Handle ordering if setup helpers can be called in any sequence.
- Make setup sibling creation idempotent when multiple fluent calls can request the same helper.

DON'T:

- Don't include setup siblings in publish manifests.
- Don't rely on user call order when setup resources have dependencies.

Canonical setup sibling shape:

```csharp
if (!resource.ApplicationBuilder.ExecutionContext.IsPublishMode)
{
    var setupName = $"{resource.Resource.Name}-setup";

    if (resource.ApplicationBuilder.TryCreateResourceBuilder<SetupResource>(setupName, out var existingSetup))
    {
        configureSetup?.Invoke(existingSetup);
        return resource;
    }

    var setupBuilder = resource.ApplicationBuilder.AddResource(new SetupResource(setupName, resource.Resource.WorkingDirectory))
        .WithParentRelationship(resource.Resource)
        .ExcludeFromManifest();

    resource.WaitForCompletion(setupBuilder);
    configureSetup?.Invoke(setupBuilder);
}
```

Use setup siblings only for work that must complete before the target starts. For post-ready background work such as model downloads or service-side child creation, prefer `ResourceReadyEvent`/`OnResourceReady`, publish status with `ResourceNotificationService`, and do not block the primary resource unless readiness truly depends on the helper.

## Admin/dev companions

Admin UIs and development tools are companion resources, not deployment resources.

DO:

- Add companions with `With{Tool}` on the parent resource.
- Return the original parent builder from `With{Tool}`.
- Use a container resource for the tool.
- Configure image, registry, endpoint, environment, health check if appropriate.
- Add parent/custom relationships.
- Call `.ExcludeFromManifest()`.
- Avoid duplicates when the companion is singleton-style.

DON'T:

- Don't expose admin UIs to publish/deploy output by default.
- Don't make a companion a top-level required resource.
- Don't hardcode host ports; provide `WithHostPort(int? port)` when fixed ports are useful.

## Standalone utility containers

Some tools are useful independently or can serve multiple resources, so they should be top-level utilities rather than parent-scoped companions.

DO:

- Use top-level `Add{Tool}` when one tool instance can connect to many resources, such as a database admin UI.
- Return an existing resource builder when repeat calls intentionally share a singleton tool.
- Still call `.ExcludeFromManifest()` when the utility is run-only.
- Provide `WithHostPort(int? port)` for tools where a fixed dashboard port is useful.

DON'T:

- Don't force singleton utility tools into `With{Tool}` APIs on every possible parent.
- Don't create duplicates from repeat `Add{Tool}` calls unless multiple tool instances are explicitly supported.
