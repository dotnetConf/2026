# Taking Cake 7 to Eleven: Pipelines as Code with C# and .NET 11

by [Mattias Karlsson](https://github.com/devlead)

- [Mastodon](https://mastodon.social/@devlead)
- [Bluesky](https://bsky.app/profile/devlead.se)
- [GitHub](https://github.com/devlead)
- [LinkedIn](https://www.linkedin.com/in/devlead)

## Abstract

Most CI/CD pipelines still live in vendor-specific YAML: powerful enough to get the job done, but often awkward to reuse, test, and debug. What if your pipeline were simply a C# program?

In this session, we will take Cake 7 all the way to eleven with the Cake SDK and .NET 11. Using familiar C# constructs, APIs, and tooling, we will build a real pipeline that runs consistently on a developer machine, in a container, and on hosted CI/CD platforms.

Along the way, you will see how pipelines written in C# can provide fast local feedback, proper debugging, reusable components, and testable build logic, without giving up integration with platforms such as GitHub Actions, Azure DevOps, or GitLab.

You will learn how to:

- Define build and release pipelines as strongly typed C# code
- Run and debug the same pipeline locally and in CI
- Structure pipeline logic for reuse, testing, and maintainability
- Keep your delivery process portable across platforms, clouds, and operating systems

By the end of the session, you will know how Cake 7 and .NET 11 can turn pipeline development up to eleven: less YAML, shorter feedback loops, and one pipeline that can build anywhere and deploy everywhere.

## Resources

- Presentation (web): https://dotnetconf2026.azurevoodoo.net/
- Slides (PowerPoint): [TakingCake7toEleven.pptx](./TakingCake7toEleven.pptx)
- Example project: [azurevoodoo/dotnetconf2026](https://github.com/azurevoodoo/dotnetconf2026)
- Cake: https://cakebuild.net/
