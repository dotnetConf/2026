# What's new in the MCP C# SDK

**Speaker:** Stephen Halter

[Session details](https://dotnet.microsoft.com/en-us/conf/2026/agenda/brk021)

[Download the slides](whats-new-in-mcp-csharp-sdk.pptx)

The Model Context Protocol is evolving, and the MCP C# SDK is evolving with it. In this session, we'll explore what's new in the SDK and the latest protocol revision, from building scalable servers with stateless requests to collecting user input across multiple interactions. See these features in action in a practical C# server, and learn how the SDK continues to support existing MCP clients as the protocol evolves.

## Demos

The [code](code) folder contains the demos shown in the session. See its [README](code/README.md) for build instructions and the stage runbook.

- [Fireworks Server](code/samples/FireworksServer/README.md) - tools, structured content, MCP Apps, prompts, resource templates, stdio, and stateless HTTP.
- [Stateless Release Planner](code/samples/ReleasePlannerServer/README.md) - multi-round tool requests (MRTR), elicitation compatibility for older clients, client-carried state, and dynamic tools with `tools/list_changed`.
- [Fireworks Chat Client](code/samples/FireworksChatClient/README.md) - using MCP tools from code with a `Microsoft.Extensions.AI` `IChatClient`.

## Resources

- [Build a minimal MCP server with .NET (quickstart)](https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/build-mcp-server)
- [MCP C# SDK source](https://github.com/modelcontextprotocol/csharp-sdk)
- [MCP C# SDK docs](https://csharp.sdk.modelcontextprotocol.io)
- [MCP specification](https://modelcontextprotocol.io/specification)
