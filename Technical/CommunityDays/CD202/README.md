# Building an AI-Powered Community Platform with Blazor, .NET MAUI, Aspire, and Azure AI Foundry

by [Augustine Correa](https://github.com/indcoder) ([Hackerspace Mumbai](https://github.com/HackerspaceMumbai))

> Azure AI Foundry is now **Microsoft Foundry**. The session title keeps the original product name.

## Abstract

Developer communities run across disconnected systems. Registration happens in Luma or Meetup, conversations happen in Teams or Discord, contributions live on GitHub, and volunteering often lives in a spreadsheet or an organizer's memory. Each tool records its own transaction, but none of them shows one person's community journey.

[Bethuya](https://github.com/HackerspaceMumbai/bethuya) is Hackerspace Mumbai's attempt to build a **Community Operating System** around people's journeys rather than around a collection of events. This session shows how the domain shaped the architecture: a **Unified Participation Ledger** of evidence and context, rich web experiences with Blazor, focused mobile experiences with Blazor Hybrid and .NET MAUI, Aspire as the operational spine, and bounded, explainable AI capabilities backed by Microsoft Foundry.

> **Start with what happened. Then choose the technology that serves it.**

## Key Takeaways

- **The Unified Participation Ledger.** An append-only history of meaningful participation (attending, contributing, volunteering, mentoring, speaking, organizing). Each record carries its evidence, provenance, consent, visibility, and verification state.
- **Evidence over scores.** Don't collapse participation into an opaque "community score". Record what happened, keep the context, and let people make the judgment.
- **Let the domain shape the architecture.** Each layer of the .NET stack serves the ledger:
  - **Blazor**: rich web experiences
  - **Blazor Hybrid + .NET MAUI**: focused mobile experiences
  - **Application services**: domain rules, authorization, and verification
  - **Microsoft Foundry**: bounded AI capabilities over grounded evidence
  - **Aspire**: model, run, and observe the distributed application
- **Aspire is the operational spine.** Compose, observe, and operate: one command starts the system, and one trace explains the workflow.
- **One Blazor experience, two hosts.** Share Razor components, models, validation, and domain contracts across Blazor Web and a .NET MAUI Blazor Hybrid host. Use mobile where mobility adds value.
- **Grounded AI, bounded action.** Ground the model in application evidence, give AI a bounded job, and make the result explainable. *Evidence is deterministic. Explanation is generative. The decision remains human.*
- **Design shifts made while building Bethuya:**
  - Events → Journeys
  - Chatbot → Workflow
  - Hidden navigation → API authorization
  - Native everywhere → Focused mobile
  - Logs later → Traces first

## Technologies Covered

- Blazor and Blazor Hybrid
- .NET MAUI
- Aspire
- Microsoft Foundry (formerly Azure AI Foundry)
- PostgreSQL

## Resources

- Slides (PowerPoint): [Bethuya-DotNetConf-2026-Public.pptx](./Bethuya-DotNetConf-2026-Public.pptx)
- Companion blog post (detailed write-up of the session): [Building an AI-Powered Community Platform: The Architecture Behind Bethuya](https://hackmum-blog.augcor.workers.dev/blog/bethuya-dotnet-conf-2026/)
- Source code: [HackerspaceMumbai/bethuya](https://github.com/HackerspaceMumbai/bethuya)
- Further reading:
  - [Bethuya's human-AI curation journey](https://hackmum-blog.augcor.workers.dev/blog/bethuya-human-ai-curation-system/)
  - [Agent workflows with Foundry and Aspire](https://hackmum-blog.augcor.workers.dev/blog/bethuya-foundry-hosted-agents-aspire/)
