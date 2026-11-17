# Stateless Release Planner

A small stdio MCP server for Visual Studio that collects a release plan over two interactive rounds using Multi Round-Trip Requests (MRTR), with all progress carried by the client instead of retained in server memory.

## Features

- `plan_release` explicitly returns `ResultOrAlternate<CallToolResult>` with an `InputRequiredResult` alternate for release controls and approval.
- Each retry carries opaque Base64 `requestState`; the handler stores no conversation state.
- The SDK compatibility layer can present the flow through elicitation in current-protocol clients.
- `unlock_production_deploy` adds `deploy_release` at runtime and triggers `notifications/tools/list_changed`.
- `deploy_release` is a simulation. It never changes an environment.

## Prerequisites

- [.NET 11 SDK](https://dotnet.microsoft.com/download/dotnet/11.0)
- An MCP host with MRTR or compatible form elicitation; Visual Studio 2026 18.12 or later is recommended

## Run independently

From the repository root:

```powershell
dotnet restore samples\ReleasePlannerServer\ReleasePlannerServer.csproj
dotnet run --project samples\ReleasePlannerServer\ReleasePlannerServer.csproj
```

The process communicates over stdio and normally waits without printing user-facing output. Stop it with <kbd>Ctrl</kbd>+<kbd>C</kbd>. To exercise the tools, connect it from an MCP host. The root `.mcp.json` configures Visual Studio after a Debug build:

```powershell
dotnet build --configuration Debug samples\ReleasePlannerServer\ReleasePlannerServer.csproj
```

## Visual Studio stage flow

1. Trust and start **Stateless Release Planner**.
2. Ask: `Use plan_release to prepare version 2.0.0 of Contoso.Api for production.`
3. In the first form choose `production`, `canary`, `10` for the initial rollout percentage, and `1` for the rollback error-rate threshold.
4. In the second form leave **Approve release plan** enabled and submit.
5. Ask: `Call unlock_production_deploy.`
6. Reopen the tool picker, then ask: `Use deploy_release to deploy version 2.0.0 of Contoso.Api.` Approve the destructive tool call.

The final deployment response explicitly states that no environment was changed.

## MRTR migration pattern

The stateful pattern awaits a callback while the handler remains alive:

```csharp
ElicitResult result = await server.ElicitAsync(...);
```

[`LegacyReleasePlannerTools.cs`](LegacyReleasePlannerTools.cs) shows the same release plan written this way for comparison. It is not registered in `Program.cs`, so the stage demo exposes only the stateless `plan_release`.

The MRTR pattern explicitly returns an input-required alternate and resumes from client-carried state:

```csharp
return ResultOrAlternate<CallToolResult>.FromAlternate(
    new InputRequiredResult
    {
        InputRequests = new Dictionary<string, InputRequest> { /* form request */ },
        RequestState = EncodeState(progress),
    },
    inputRequiredResultTypeInfo);
```

The attributed tool keeps its normal discovery schema while an alternate-result call-tool filter supplies this explicit return path. On retry, the implementation reads `context.Params.InputResponses` and `context.Params.RequestState`. The sample validates decoded state as untrusted input. Production servers should also integrity-protect client-carried state when it affects authorization.

If forms do not appear, update the host and confirm it supports MRTR or form elicitation. If `deploy_release` does not appear after unlocking, reopen or refresh the tool picker so the host processes `notifications/tools/list_changed`.
