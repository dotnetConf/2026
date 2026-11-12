# No XAML Required: Native Windows Apps in Pure C#

**Session:** BRK027 | **Speaker:** [Alexandre Zollinger Chohfi](https://dotnet.microsoft.com/en-us/conf/2026/speakers/alexandre-zollinger-chohfi)

[Session details](https://dotnet.microsoft.com/en-us/conf/2026/agenda/brk027) | [Download the slides](Reactor-DotNetConf-2026.pptx) | [Microsoft.UI.Reactor on GitHub](https://github.com/microsoft/microsoft-ui-reactor)

## Abstract

What if a complete native Windows app were a single C# file - no .csproj, no App.xaml, no code-behind, no ViewModels? Microsoft.UI.Reactor is an experimental C# framework that brings the MVU (Model-View-Update) pattern to WinUI 3. Your UI is a function of your state. State lives in hooks, next to the code that uses it. And every control on screen is a real WinUI control - the same runtime, the same controls, the same theming as the XAML app next door. We'll start by running a complete Windows app with dotnet run app.cs, then build up: hooks and state, lists and keyed reconciliation, and the 60+ Roslyn analyzers that catch UI bugs while you type - because when your UI is ordinary C#, the entire .NET toolchain points straight at it. This is an experiment, and the talk is honest about that: what works, what's rough, what we got wrong, and where XAML and MVVM fit alongside it (spoiler: they aren't going anywhere). Come for the live demo, leave with an opinion - we're actively collecting them.
