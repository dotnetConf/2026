# .NET Conf MCP Stage Runbook

Build `dotnet-conf-mcp-demos.slnx` in Debug, trust both servers from `.mcp.json`, and open <http://localhost:5399> before starting.

## Fireworks

1. Add `choreograph_fireworks`.
2. Set `occasion` to `.NET Conf 2026 closing celebration`.
3. Set `mood` to `triumphant`.
4. Approve `launch_fireworks`.
5. Use the synchronized SignalR browsers as the reliable stage output. Inline MCP Apps rendering is optional and host-dependent.

## Release Planner

1. Ask: `Use plan_release to prepare version 2.0.0 of Contoso.Api for production.`
2. Choose `production`, `canary`, `10`, and `1`.
3. Leave **Approve release plan** enabled and submit.
4. Show `ExecutePlanRelease` returning `ResultOrAlternate<CallToolResult>` and `RequestInput` returning an `InputRequiredResult` alternate instead of throwing.
5. Show the final `Stateless proof` line.
6. Ask: `Call unlock_production_deploy.`
7. Reopen the tool picker.
8. Ask: `Use deploy_release to deploy version 2.0.0 of Contoso.Api.`
9. Approve the tool and emphasize that the deployment is simulated.

If the inline Fireworks app or Release Planner forms are unavailable, use the browser output for Fireworks and narrate the MRTR sequence from the tool descriptions. Neither demo requires cloud access.
