# Meeple-Driven Design: Modeling Board Game Domains with C# 15 & .NET 11

Presented by [Sarah Dutkiewicz](https://linkedin.com/in/sadukie) 

How do you model a game turn when a player can either roll dice, play a card, or pass without relying on vague string flags, null-heavy wrapper classes, or unsafe casting? In this fast-paced 30-minute session, we’ll take a trip to the tabletop. We will use real-world board game mechanics - such as player turns, tile placement validations, and victory conditions - to explore the newest domain modeling capabilities in C# 15 and .NET 11. You'll learn how native Discriminated Unions replace custom result wrappers, how Closed Hierarchies force exhaustive compile-time checking across game actions, how System.Text.Json handles complex union state out of the box, and how EF Core 11 indexes deeply nested complex types without schema friction. Walk away with cleaner, safer C# patterns you can apply directly to your software - no board game experience required!

_This 30-minute .NET Conf presentation is based off of a 6-post blog series on the NimblePros blog._

## Related Links

- ▶️ [Tiles & Tokens rules - NimblePros YouTube](https://youtu.be/Ys4RChDxhQw)
- 💻 [Tiles & Tokens GitHub repo](https://github.com/NimblePros/tiles-and-tokens-net-11)

### Related YouTube Videos
  
- ▶️ [Tiles & Tokens series - NimblePros YouTube](https://www.youtube.com/watch?v=kEpWqcj8pmU&list=PLKnPPwee7eu4)
  - ▶️ [Part 1: Rolling into .NET: Modeling a Board Game with C# 15](https://youtu.be/kEpWqcj8pmU)
  - ▶️ [Part 2: Discriminated Unions in C# 15: A Turn is Exactly One Thing](https://youtu.be/n7P1jkv9v7Q)
  - ▶️ [Part 3: Closed Hierarchies in C# 15: Every Victory Condition Accounted For](https://youtu.be/_XOQdt2GOU0)
  - ▶️ [Part 4: Saving the Game: System.Text.Json and Union State](https://youtu.be/rPGXUdBwUDc)
  - ▶️ [Part 5: EF Core 11: Querying Deeply Nested Game State Without Schema Friction](https://youtu.be/GdQFFh0xVfk)

### Related Blog Series

- 📝 [Tiles & Tokens blog series - NimblePros bloh](https://blog.nimblepros.com/series/tiles-and-tokens/)
  - 📝 [Rolling Into .NET 11: Modeling a Board Game with C# 15[(https://blog.nimblepros.com/blogs/rolling-into-dotnet-11-modeling-a-board-game-with-csharp-15/)
  - 📝 [Discriminated Unions in # 15: A Turn is Exactly One Thing](https://blog.nimblepros.com/blogs/discriminated-unions-in-csharp-15-a-turn-is-exactly-one-thing/)
  - 📝 [Closed Hierarchies in C# 15: Every Victory Condition Accounted For](https://blog.nimblepros.com/blogs/closed-hierarchies-in-csharp-15-every-victory-condition-accounted-for/)
  - 📝 [Saving the Game: System.Text.Json and Union State](https://blog.nimblepros.com/blogs/saving-the-game-system-text-json-and-union-state/)
  - 📝 [EF Core 11: Querying Deeply Nested Game State Without Schema Friction](https://blog.nimblepros.com/blogs/ef-core-11-querying-deeply-nested-game-state/)
  - 📝 [The Bonus Round: Publishing Tiles & Tokens with Native AOT](https://blog.nimblepros.com/blogs/the-bonus-round-publishing-tiles-and-tokens-with-native-aot/)
