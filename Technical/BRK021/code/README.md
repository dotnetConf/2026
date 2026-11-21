# .NET Conf MCP Demos

Two standalone C# MCP servers built for a reliable .NET Conf stage demonstration. They show synchronized browser output, MCP Apps metadata, prompts, resources, stateless multi-round input, and live tool discovery without depending on the C# SDK source tree, npm, a cloud service, or conference Wi-Fi.

| Demo | Stage moment | MCP features |
| --- | --- | --- |
| [Fireworks Server](samples/FireworksServer/README.md) | One approved tool call launches synchronized fireworks in every audience browser. | Tools, structured content, MCP Apps, prompts, resource templates, stdio, stateless HTTP |
| [Stateless Release Planner](samples/ReleasePlannerServer/README.md) | Visual Studio gathers a release plan through familiar forms, then discovers a deployment tool live. | MRTR, elicitation compatibility, client-carried state, `tools/list_changed`, dynamic tools |

An optional [Fireworks Chat Client](samples/FireworksChatClient/README.md) shows the same Fireworks tools used from code. It passes `ListToolsAsync()` results to a `Microsoft.Extensions.AI` `IChatClient` backed by GPT-6 Luna on Azure OpenAI. Unlike the servers, it needs an Azure subscription and network access.

> [!IMPORTANT]
> The Fireworks SignalR browser displays are the reliable visual output. MCP Apps rendering depends on host support and is not guaranteed in Visual Studio. The Release Planner's deployment tool is only a simulation and never changes an environment.

## Prerequisites

- [.NET 11 SDK](https://dotnet.microsoft.com/download/dotnet/11.0)
- Visual Studio 2026 18.12 or later with GitHub Copilot and MCP server support for the documented stage flow
- A modern browser for the Fireworks audience display

The projects use public `ModelContextProtocol` packages pinned to `2.4.0` in `Directory.Packages.props`. The MCP Apps integration and the Release Planner's generic alternate-result APIs remain experimental.

## Restore and build

From the repository root:

```powershell
dotnet restore dotnet-conf-mcp-demos.slnx
dotnet build --configuration Debug dotnet-conf-mcp-demos.slnx
```

The Debug build creates the assemblies referenced by `.mcp.json`. Rebuild after source changes, then restart the affected MCP server.

## Run outside Visual Studio

Run Fireworks in Streamable HTTP mode:

```powershell
dotnet run --project samples\FireworksServer\FireworksServer.csproj
```

Open <http://127.0.0.1:5399> for the audience display and connect an MCP client to <http://127.0.0.1:5399/mcp>.

Run the Release Planner's stdio server:

```powershell
dotnet run --project samples\ReleasePlannerServer\ReleasePlannerServer.csproj
```

It waits for an MCP client on standard input/output and normally has no interactive console UI. See each demo README for independent configuration and troubleshooting.

With Fireworks running in HTTP mode, the [Fireworks Chat Client](samples/FireworksChatClient/README.md) can launch a show from code after its Azure OpenAI user secrets are configured:

```powershell
dotnet run --project samples\FireworksChatClient\FireworksChatClient.csproj
```

## Visual Studio MCP configuration

1. Open `dotnet-conf-mcp-demos.slnx`.
2. Build the solution in the **Debug** configuration.
3. Open Visual Studio's MCP server manager. The root `.mcp.json` registers **Fireworks Show Control** and **Stateless Release Planner**.
4. Trust and start both local servers.
5. After rebuilding a server, stop and restart it in the manager. Saving `.mcp.json` can also prompt Visual Studio to reload the configuration.

The checked-in configuration starts the Fireworks MCP transport over stdio while its ASP.NET Core host serves the browser display at `http://localhost:5399`. Release Planner uses stdio only.

## Exact stage runbook

### 1. Synchronized Fireworks

Before the audience arrives, start **Fireworks Show Control** and open <http://localhost:5399> in one or more browser windows.

1. Add the `choreograph_fireworks` prompt in Visual Studio.
2. Set `occasion` to `.NET Conf 2026 closing celebration`.
3. Set `mood` to `triumphant`.
4. Submit the prompt and approve the resulting `launch_fireworks` tool call.
5. Point out that the tool's structured result drives both any supported inline MCP App and every connected SignalR browser.

If inline MCP Apps rendering is unavailable, continue with the browser displays; that is the expected and reliable fallback.

### 2. Stateless Release Planner

1. Ask exactly: `Use plan_release to prepare version 2.0.0 of Contoso.Api for production.`
2. Complete the first form with:

   | Field | Value |
   | --- | --- |
   | Target environment | `production` |
   | Rollout strategy | `canary` |
   | Initial rollout percentage | `10` |
   | Rollback error-rate threshold | `1` |

3. In the second form, leave **Approve release plan** enabled and submit it.
4. Show the explicit `ResultOrAlternate<CallToolResult>` path that returns each `InputRequiredResult` with its form request and opaque state.
5. Point out the final `Stateless proof` line: the handler reconstructed the plan from client-carried `requestState`.
6. Ask exactly: `Call unlock_production_deploy.`
7. Reopen the tool picker and show the newly discovered `deploy_release`.
8. Ask exactly: `Use deploy_release to deploy version 2.0.0 of Contoso.Api.`
9. Approve the destructive tool call and highlight `SIMULATED PRODUCTION DEPLOYMENT` and `No real environment was changed`.

A condensed presenter checklist is available in [the stage runbook](samples/dotnet-conf-mcp-demos.md).

## Architecture

| Component | Runtime path | Responsibility |
| --- | --- | --- |
| Visual Studio | `.mcp.json` → stdio | Discovers prompts, resources, and tools; renders forms; handles approvals |
| Fireworks MCP server | stdio or `/mcp` | Creates structured show plans and MCP Apps metadata |
| Fireworks ASP.NET Core host | `http://localhost:5399` | Serves the embedded static audience page and SignalR hub |
| Audience browsers | SignalR WebSocket | Receive the same show payload and launch in sync |
| Release Planner | stdio | Reconstructs each round from opaque client-carried state |
| Dynamic tool collection | `notifications/tools/list_changed` | Adds the simulated `deploy_release` tool without a restart |

Both servers are local processes. Fireworks is intentionally dual-purpose: stdio remains the MCP control channel while HTTP/SignalR supplies dependable stage visuals.

## Troubleshooting and fallbacks

| Symptom | Resolution |
| --- | --- |
| Visual Studio reports a missing server assembly | Build `dotnet-conf-mcp-demos.slnx` in **Debug** from the repository root, then restart the server. |
| Port 5399 is already in use | Change both Fireworks `ListenUrl` and `DashboardUrl` in `.mcp.json`, then reload the server and browse to the new port. |
| Browser says `RECONNECTING` | Confirm Fireworks is running, use the configured dashboard URL, and verify local WebSockets are not blocked by a firewall or proxy. |
| MCP App does not render inline | This is host-dependent. Use the SignalR browser display, which is the supported presentation fallback. |
| Release forms do not appear | Confirm the MCP host supports MRTR or compatible form elicitation and update Visual Studio if needed. |
| `deploy_release` is not visible | Reopen or refresh the tool picker after `unlock_production_deploy` so the host processes `notifications/tools/list_changed`. |
| Conference network is unreliable | Keep all displays on the presenter machine. No network beyond loopback is required. |

For a rehearsed private-LAN Fireworks display, follow the opt-in binding and firewall guidance in the [Fireworks README](samples/FireworksServer/README.md). Do not expose the listener beyond the intended private network.

## Repository layout

```text
.mcp.json
Directory.Packages.props
dotnet-conf-mcp-demos.slnx
samples/
  FireworksChatClient/
  FireworksServer/
  ReleasePlannerServer/
  dotnet-conf-mcp-demos.md
```
