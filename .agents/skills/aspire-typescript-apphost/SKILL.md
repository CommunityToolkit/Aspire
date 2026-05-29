---
name: aspire-typescript-apphost
description: Generate a TypeScript AppHost from an existing C# example AppHost, then add a TypeScriptAppHostTest-based integration test.
---

# Aspire TypeScript AppHost Generator

Use this skill when the user wants to create a TypeScript AppHost (`apphost.mts`) from an existing C# example AppHost and add/update the corresponding integration test.

## Prompting rule

- Ask for the integration **only when this skill is actively running** and only if the user did not provide it.
- Do not ask for integration selection before this skill is invoked.

## Goal

For one integration, produce:

1. A TypeScript AppHost example beside the existing C# AppHost example.
2. A test in the integration's `tests/CommunityToolkit.Aspire.Hosting.<Integration>.Tests` project that uses `TypeScriptAppHostTest.Run(...)`.

## Workflow

1. Resolve the integration
   - If the user already specified an integration, use it.
   - Otherwise, prompt for it at runtime.

2. Locate the source AppHost and test project
   - Source C# example path pattern:
     - `examples/<example>/CommunityToolkit.Aspire.Hosting.<Integration>.AppHost/Program.cs`
   - Target TypeScript example path pattern:
     - `examples/<example>/CommunityToolkit.Aspire.Hosting.<Integration>.AppHost.TypeScript/`
   - Test project path:
     - `tests/CommunityToolkit.Aspire.Hosting.<Integration>.Tests/`

3. Create or update the TypeScript AppHost folder
   - Required files:
     - `apphost.mts`
     - `aspire.config.json`
     - `package.json`
     - `tsconfig.json`
     - `eslint.config.mjs`
     - `package-lock.json`
   - Keep template/config conventions aligned with existing TypeScript AppHost examples in this repo.
   - Use TypeScript API method names generated for Aspire exports (for overloads, use the exported TypeScript name, not guessed C# casing).

4. Translate C# AppHost logic to TypeScript
   - Preserve resource naming and behavior where practical.
   - Use TypeScript Aspire methods and options shapes.
   - Keep path handling and package mapping valid for the example location.

5. Add the integration TypeScript AppHost test
   - Add `TypeScriptAppHostTests.cs` in the integration test project if missing.
   - Use:
     - `TypeScriptAppHostTest.Run(...)`
     - `appHostProject` = `<Integration>.AppHost.TypeScript` folder name
     - `packageName` = integration package name
     - `exampleName` = example folder name
     - `waitForResources` = resources required to verify startup in Aspire
   - Follow existing test style and attributes (`[RequiresDocker]` when applicable).

6. Validate changes
   - Run the relevant test project to ensure the TypeScript AppHost compiles and starts via the shared validation flow.

## Guardrails

- Do not edit generated `.aspire/modules/` files.
- Do not change unrelated examples or tests.
- Keep all changes scoped to the selected integration plus the skill files.
