## Shipping a VS Code extension backed by a .NET Tool based analyzer

Erik Ejlskov Jensen 

VS Code speaks TypeScript, but my T-SQL analysis rules are C# on top of DacFx. So how do you light up 140+ .NET analyzer rules as the developer types — no build step, nothing to manually install? In this talk I'll show how I built the T-SQL Analyzer VS Code extension as a thin TypeScript front-end (with no previous TypeScript experience) over a long-lived .NET back-end: a persistent analyzer process, a simple stdin/stdout JSON protocol, and distribution as a NuGet tool run via dnx (so users just need the .NET 11 SDK). I'll share the real lessons learned — so you can put a friendly editor experience on top of any .NET library you already own. 

Takeaways 
- Architect an editor extension as a thin UI over a reusable .NET engine
- Design a minimal, robust stdin/stdout JSON protocol
- Run and supervise a long-lived .NET server (spawn, crash recovery, shutdown)
- Distribute via NuGet + dnx for zero-install users •-
- Turn analysis results into live diagnostics with good error UX

[Presentatio](https://github.com/ErikEJ/SqlServer.Rules/blob/master/talks/from-dotnet-rules-to-live-tsql-feedback.pdf)

[GitHub repository](https://github.com/ErikEJ/SqlServer.Rules)
