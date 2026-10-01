---
name: client-integration-authoring
description: Guides authoring and reviewing CommunityToolkit Aspire client integrations, including registration shape, configuration, keyed services, health checks, telemetry, READMEs, examples, and tests.
---

# Aspire client integration authoring

Use this skill when creating, modifying, or reviewing `CommunityToolkit.Aspire.*` client integration packages whose extension methods register consuming-application services.

## First step: classify the integration

Inspect nearby client integrations and classify the requested work:

1. **Client lifetime:** singleton, scoped, transient, SDK-managed, or `HttpClient`-backed.
2. **Configuration source:** connection string, configuration section, explicit settings, or a combination.
3. **Registration shape:** default service, keyed service, custom service key, or a fluent builder.
4. **Optional concerns:** health checks, tracing/metrics, Microsoft.Extensions.AI adapters, Native AOT, or multiple logical clients.

Do not force every integration into one template. Read [patterns](references/patterns.md) and apply only the capabilities supported by the underlying client library and demonstrated by nearby integrations.

## Authoring workflow

1. Inspect two existing client integrations with the closest registration and configuration shape.
2. Define the smallest public API that follows the underlying SDK and repository conventions.
3. Implement settings binding, connection-string override, and the configuration callback in a predictable order.
4. Add default and keyed registration only when both are useful and can behave consistently.
5. Add health checks and telemetry only when the integration can implement them reliably.
6. Add focused tests and documentation using [testing and documentation](references/testing-and-documentation.md).
7. Add source, test, and example projects to `CommunityToolkit.Aspire.slnx` when new projects are created.
8. Validate with the narrowest relevant build and test commands.

## Non-negotiable conventions

- Client extension methods use the `Microsoft.Extensions.Hosting` namespace.
- Client packages use `CommunityToolkit.Aspire.[IntegrationName]` naming and include the `client` package tag.
- Public APIs require XML documentation.
- Validate public inputs with the throw helpers used by nearby integrations.
- Do not create or manually edit generated `*/api/*.cs` files.
- Fail with a clear configuration error when a client cannot be constructed; do not silently create an invalid client.
- Keep health checks lightweight and cancellation-aware.
- Avoid adding health-check, telemetry, or HTTP dependencies when the integration does not use them.

## Review workflow

Review for behavioral correctness rather than template conformity:

- configuration precedence and missing-configuration behavior;
- service lifetime and keyed/default symmetry;
- duplicated registrations or health checks;
- disposal and `HttpClient` usage;
- telemetry opt-out behavior;
- health-check names, timeouts, and failure handling;
- README examples that match the actual API; and
- tests that cover the supported registration shapes.
