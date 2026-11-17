# Fireworks Server

A local MCP server that launches the same animated fireworks show in an MCP App and every connected browser. The reliable presentation output is the SignalR browser display; inline MCP Apps rendering is available only when the MCP host supports it.

## Features

- `launch_fireworks` returns structured show data and broadcasts it through SignalR.
- `choreograph_fireworks` turns an occasion and mood into a tool-ready creative brief.
- `show://fireworks/palettes/{theme}` exposes a parameterized palette resource.
- The server supports stdio MCP alongside its browser dashboard, or stateless Streamable HTTP MCP.
- The audience page includes a small SignalR JSON-protocol client and has no npm, CDN, cloud, or internet runtime dependency.

## Theme identities

- **Aurora** — drifting launches, flowing cyan/green/violet curtains, and a softly animated polar-sky backdrop.
- **Cyberpunk** — rail launches, geometric neon bursts, a perspective grid, angular city silhouettes, and a restrained scanline.
- **Ocean** — bubble launches, expanding ripple rings, deep teal water, and layered wave motion.
- **Sunset** — horizon comets and fans, warm embers, a setting sun, and dark landscape silhouettes.
- **Celebration** — rapid salvos, multicolor confetti and streamers, festival lighting, and a banner-like finale.

Show inputs generate stable choreography and particle motion for repeatable rehearsals; the show ID remains unique for each launch.

## Prerequisites

- [.NET 11 SDK](https://dotnet.microsoft.com/download/dotnet/11.0)
- An MCP host for tool calls; Visual Studio 2026 18.12 or later is recommended for the stage flow

## Run independently

From the repository root:

```powershell
dotnet restore samples\FireworksServer\FireworksServer.csproj
dotnet run --project samples\FireworksServer\FireworksServer.csproj
```

Open <http://127.0.0.1:5399>. The Streamable HTTP MCP endpoint is <http://127.0.0.1:5399/mcp>.

To use stdio while keeping the dashboard available:

```powershell
dotnet run --project samples\FireworksServer\FireworksServer.csproj -- --McpTransport=stdio --ListenUrl=http://127.0.0.1:5399 --DashboardUrl=http://localhost:5399
```

The root `.mcp.json` uses this mode for Visual Studio. Build the Debug configuration before starting it:

```powershell
dotnet build --configuration Debug samples\FireworksServer\FireworksServer.csproj
```

## Visual Studio stage flow

1. Trust and start **Fireworks Show Control** in the MCP server manager.
2. Open <http://localhost:5399> in every audience browser.
3. Add the `choreograph_fireworks` prompt with `occasion` set to `.NET Conf 2026 closing celebration` and `mood` set to `triumphant`.
4. Approve `launch_fireworks`. Every connected browser launches the same show.

Visual Studio is not guaranteed to render MCP Apps inline. Treat the SignalR browsers as the stage output; MCP Apps rendering is an optional host-dependent enhancement.

## Configuration

The defaults are `McpTransport=http`, `ListenUrl=http://127.0.0.1:5399`, and `DashboardUrl` equal to the listen URL. Override them after `--`, for example:

```powershell
dotnet run --project samples\FireworksServer\FireworksServer.csproj -- --ListenUrl=http://127.0.0.1:5400 --DashboardUrl=http://localhost:5400
```

For a rehearsed private-LAN demo, use `--ListenUrl=http://0.0.0.0:5399`, allow the port through the private-network firewall, and open `http://<presenter-ip>:5399` on audience devices. This exposes the read-only audience feed to the local network; restore loopback binding afterward.

If port 5399 is occupied, choose another port in both `ListenUrl` and `DashboardUrl`. If the MCP host starts but browsers do not connect, confirm the URL, firewall profile, and WebSocket availability.
