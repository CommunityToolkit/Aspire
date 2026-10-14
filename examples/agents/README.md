# AI agents example

This example keeps all six extracted multi-protocol samples:

| Resource | Implementation and endpoints |
| --- | --- |
| `a2a-jsonrpc-agent` | Python, LangChain, official A2A SDK; JSON-RPC `/`, `/.well-known/agent-card.json`, `/health` |
| `responses-agent` | .NET Microsoft Agent Framework, OpenAI Responses API, MCP `/mcp`, `/health` |
| `vercel-ai-responses-agent` | TypeScript Vercel AI SDK, Responses-compatible `/v1/responses`, `/health` |
| `mcp-agent` | .NET MCP `/mcp`, model-backed `answer_question`, `/health` |
| `agui-acp-agent` | Python Microsoft Agent Framework, AG-UI `/ag-ui`, ACP `/runs`, `/health` |
| `agent-env-dump` | Prints the injected A2A agent-card URL; stays running |

The AppHost references the local `src/CommunityToolkit.Aspire.Hosting.Agents`
project and public Aspire packages. SDK and Aspire versions follow the Toolkit's
central configuration. Foundry uses the repository's Aspire preview suffix
because no stable `Aspire.Hosting.Foundry` 13.6.1 package exists. Sample-specific
dependencies are centrally managed in `Directory.Packages.props` in this folder;
the newer MCP sample dependency does not upgrade unrelated Toolkit examples.

## Prerequisites

- .NET SDK **10.0.401** or a compatible .NET 10 SDK.
- Aspire CLI **13.6.x** for the lifecycle commands below.
- Node.js **20 or newer**, with npm. The preserved `package-lock.json` selects
  **npm**, not pnpm/yarn/bun. Validation used Node.js 24.18.0 and npm 11.16.0.
- Python **3.13** (the weather sample pins this minor version), and
  [uv](https://docs.astral.sh/uv/). Validation used Python 3.13.14 and uv 0.11.32.
  Both Python samples retain their `uv.lock` files.
- For model-backed usage only: an **already existing** Azure AI Foundry account,
  project, and GPT-4.1-mini-compatible model deployment, plus an Azure identity
  authorized to call that deployment/project. Azure CLI sign-in (`az login`) is
  one way to supply `DefaultAzureCredential`; no API keys are read by these samples.
  A resource administrator must grant the needed Foundry/project and inference
  permissions in advance (for example Azure AI User and Cognitive Services
  OpenAI User as appropriate to the existing resource).

No containers are declared in the default existing-resource graph.

## Existing Foundry configuration (no provisioning)

**Existing connections are the default.** The AppHost uses connection-string
resources and does not provision a Foundry account, project, model deployment,
or role assignments in this mode.

Obtain the project endpoint and Azure OpenAI endpoint from your existing Foundry
resource. The deployment value is the **existing deployment name**, not
necessarily its underlying model name. From the repository root, replace the
placeholders locally:

```bash
dotnet user-secrets set --project examples/agents/CommunityToolkit.Aspire.Hosting.Agents.AppHost \
  "UseExistingFoundry" "true"
dotnet user-secrets set --project examples/agents/CommunityToolkit.Aspire.Hosting.Agents.AppHost \
  "ConnectionStrings:agentsproject" \
  "Endpoint=https://YOUR-ACCOUNT.services.ai.azure.com/api/projects/YOUR-PROJECT"
dotnet user-secrets set --project examples/agents/CommunityToolkit.Aspire.Hosting.Agents.AppHost \
  "ConnectionStrings:chat" \
  "Endpoint=https://YOUR-ACCOUNT.openai.azure.com/;Deployment=YOUR-DEPLOYMENT"
```

The original AppHost `UserSecretsId`
`e1c0f8c6-8b6b-4e42-84f3-59c0135a60ce` is preserved. Environment variables
`ConnectionStrings__agentsproject` and `ConnectionStrings__chat` are alternatives
to user secrets. Do not commit credentials or resource-specific configuration.
Aspire injects these same connection-string variables into the relevant agents.

The upstream managed Foundry graph is retained behind
`UseExistingFoundry=false`. **Do not select that mode unless you explicitly intend
Azure provisioning and associated costs.** It was not executed during migration.
There are no deployment, publishing, or provisioning steps in this guide.

## Build and deterministic checks

Run from the repository root:

```bash
dotnet build examples/agents/CommunityToolkit.Aspire.Hosting.Agents.AppHost/CommunityToolkit.Aspire.Hosting.Agents.AppHost.csproj

cd examples/agents/vercel-ai-responses-agent
npm ci
npm run build
cd ../../..

cd examples/agents/weather-agent-python
uv run --locked python -m unittest discover -s tests -v
cd ../../..

cd examples/agents/agui-acp-agent-python
uv sync --locked
uv run --locked python -m compileall -q agui_acp_agent
cd ../../..
```

`npm run build` runs `tsc --noEmit`. The weather tests substitute a deterministic
agent; they exercise successful completion, model failure cleanup, and
cancellation while answering, adding an artifact, or completing a task.
They do not authenticate or invoke a real model.

## Run (only after configuring an existing resource)

These commands start services but do not themselves send a model prompt:

```bash
cd examples/agents
aspire start --non-interactive
aspire wait a2a-jsonrpc-agent
aspire wait responses-agent
aspire wait vercel-ai-responses-agent
aspire wait mcp-agent
aspire wait agui-acp-agent
aspire describe
```

Use the dynamically assigned resource URLs displayed by Aspire; do not assume
the standalone fallback `PORT=8080`. Open the dashboard URL printed by Aspire to
inspect agents and MCP tool commands. Calling an agent, or the MCP
`answer_question` tool, can invoke the paid deployment. The env-dump process
only prints the A2A card URL. Stop the app from this same directory:

```bash
aspire stop
```

The weather agent's FastAPI OpenTelemetry instrumentation and dependencies are
preserved, as are the AppHost dashboard OTLP settings. This migration does not
add instrumentation to samples that did not already have it.

## Authentication and verification boundary

Dashboard invocation commands run in the AppHost process. They do not acquire or
refresh tokens or forward the dashboard user's identity. For authenticated agent
endpoints, use a driver application that handles the required authentication;
do not disable endpoint authentication. The Azure identity used by an agent to
call Foundry is a separate authentication boundary.

The deterministic checks above do not authenticate, provision Azure resources,
change role assignments, or send paid model prompts. Live model calls, identity
permissions, remote endpoint availability, and live dashboard telemetry require
separate validation against your existing environment.

## Provenance

Ported from [microsoft/aspire#17757](https://github.com/microsoft/aspire/pull/17757)
via [standalone source `8d93031`](https://github.com/tommasodotNET/aspire-agents-integration/tree/8d93031b42666b353aee4a8370a8b5349703ad98).
Original .NET Foundation MIT license notices are retained.
