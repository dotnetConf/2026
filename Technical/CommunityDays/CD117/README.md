# What NOT to AI: Hand-Crafting Your App's Core Foundation

**.NET Conf 2026 Community Days** · Thursday, November 12, 2026 · Michael Washington

## Abstract

AI coding agents can build a whole application in days, write and run their own tests, review their own security and write the user guide. Some decisions are still yours alone: the application's design, the technology stack (authentication and database) and the UI. If you don't decide, the AI will, and it will decide by accident.

This session walks through a spec-first workflow. Prompt templates make the agent interview you, your decisions go into a master .md file and sub .md files you review, and only then does the agent write the code, test it, audit it for security, document it and deploy it. Two production apps built in October 2026, Blazor Azure Inventory and Aspire Deploy Service, show the workflow in practice, including a real AI security review whose two High findings were fixed in three days.

## Session outline

1. AI coding can do incredible things, but there are things you must do yourself
2. The workflow: prompt template, master .md, sub .md files and mockups (yours), then code, test and audit, guide and deploy (the agent's)
3. Examples from the field: Blazor Azure Inventory and Aspire Deploy Service
4. We're coding Markdown: every mistake is made in the .md file, so that's where you fix it
5. What NOT to AI: the application design, the technology stack and the UI
6. What to AI: coding, testing, security checks, fixes, the user guide and deployment
7. Audit and test everything: a real AI security review and its fixes

The prompt template used in the workflow is by Saad Awais, on BlazorHelpWebsite.com.

## Slides

- [What-Not-To-AI-Animated.pptx](What-Not-To-AI-Animated.pptx) (PowerPoint, in this folder)
- [Same deck on Dropbox](https://www.dropbox.com/scl/fi/tzdb65xr14ptnunmsj7ae/What-Not-To-AI-Animated.pptx?rlkey=eutwot3jrvp2ql6r2brxb2grt&dl=0)

## Code

- [Blazor Azure Inventory](https://github.com/ADefWebserver/Blazor-Azure-Inventory): signs in with Entra ID, inventories Azure resources, maps how web apps connect to SQL and storage as a knowledge graph, and adds an AI chat.
- [Aspire Deploy Service](https://github.com/ADefWebserver/AspireDeployService): a Windows desktop app, listed in the Microsoft Store, that deploys any Aspire 13 solution to Azure Container Apps.

## About the speaker

Michael Washington is a Principal AI Solutions Architect and Microsoft MVP. He runs BlazorHelpWebsite.com and has written several Syncfusion Succinctly books, including *Blazor Succinctly* (updated for .NET 10), *Blazor WebAssembly Succinctly* and *Custom Blazor Oqtane Modules Succinctly*, with *Lightweight Knowledge Graphs Succinctly* coming soon. He also created AIStoryBuilders, an open-source AI-assisted novel writing application.

- GitHub: https://github.com/ADefWebserver
- Blog: BlazorHelpWebsite.com
