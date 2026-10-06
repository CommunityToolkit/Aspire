---
name: Integration examples
description: Use when creating or modifying projects under examples.
applyTo: "examples/**"
---

# Example guidance

- Keep examples minimal and focused on the integration's primary usage.
- Hosting integrations should normally include an AppHost that can also support end-to-end tests.
- Follow the folder and project naming used by nearby examples for the same integration type.
- Do not add explicit versions to centrally managed package references.
- Keep development-only helper resources out of publish manifests with the established `.ExcludeFromManifest()` pattern.
- Update the integration README when example usage changes.
