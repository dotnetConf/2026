# Eliminating Hidden Dangers to Your Applications: Patterns for Reliable Systems

**[Eliminating Hidden Dangers - Short Session - Slide Deck](https://cognitiveinheritance.com/Presentations/ShortSession/EliminatingHiddenDangers/index.html)**

> ​👏👏👏 Hands down the best talk on Dual-Writes and reliable distributed messaging patterns I've ever seen! Thank you, Barry! - @JoseSilva-gt6zj

## Session Abstract

There is a danger lurking in every system that hasn't taken explicit steps to eliminate it. This under-recognized data killer can cause intermittent inconsistencies in your data which can be as difficult to find as they are to correct. Fortunately, recognizing this monster is easy, and there are a number of great techniques you can use to keep it away for good. In this talk, we will learn about the Dual-Writes anti-pattern: how to spot it, and what to do to avoid it. In doing so, we'll discuss the 4 primary patterns for keeping data consistent in modern software systems, so you can spend less time supporting your applications. We'll see examples in C# of each pattern, including demonstrations using SQL Server, Cosmos DB and Redis.

## Presented by Barry S. Stahl

Barry S. Stahl is a Solutions Architect, Developer and Microsoft .NET MVP with more than four decades of experience building reliable, scalable systems on .NET and other technologies. He has delivered hundreds of sessions at conferences, code camps, and user groups across North America, Europe, and Australia, including NDC Oslo, NDC Sydney, Craft Conference Budapest, Techorama Netherlands, and ConFoo Montreal. Barry is known for making complex topics clear, actionable, and grounded in real-world engineering practice, with talks that span architecture, problem-solving approaches, artificial intelligence, optimization algorithms, and designing systems that detect failures and recover gracefully.

Barry works as Principal Engineer for AZNerds in Phoenix, Arizona, where he has lived for more than 40 years. He co-founded [AZGiveCamp](https://www.azgivecamp.org/), a non-profit that brings volunteer developers together to build websites and apps for regional charities. He is also an Election Integrity Activist, a baseball and hockey fan, husband of one genius and father of another, and a long-time contributor to the developer community. When he is not speaking or organizing community events, Barry writes about these same topics on his blog, [Cognitive Inheritance](https://cognitiveinheritance.com/), and can be found in the Fediverse at [@bsstahl@cognitiveinheritance.com](https://fosstodon.org/@Bsstahl).

## Q & A

### With manual commit after work in Service Bus/Kafka, how do you size batches to limit duplicate work on retries?

I try to avoid premature optimization, so I usually start with commit on every message individually, then measure the impact. Until you reach very high throughput, is generally not a problem to commit every message (though it can be relatively slow). I only increase the batch size, which reduces reliability, once the performance requires it.

### Which of the isolation patterns do you recommend starting with?

It depends on what you're already using. If you have an ACID-compliant database that doesn't provide a CDC capability that meets your needs, the Outbox pattern is often the simplest place to start. If you're already using, or plan to use, a database such as Cosmos DB that provides suitable CDC support, use CDC. Otherwise, start with a messaging platform such as Azure Service Bus or Kafka.

For experimentation and greenfield services, Aspire makes it easy to spin up a service connected to a containerized Kafka instance, making messaging-based approaches straightforward to explore.

### Which of the coordination patterns do you recommend starting with?

As a rule of thumb, start with Saga Choreography and let the services coordinate themselves through events. Move to Saga Orchestration when the workflow becomes complex enough that an explicit coordinator provides more value than the overhead it introduces, particularly when dealing with compensation, retries, timeouts, or complex business processes.

### My [customer/manager/product owner] insists that exactly-once delivery is a must-have requirement for our project but I don't really think that is right. How do I convince them that at-least once delivery is the best option?

When someone says they need exactly-once delivery, what they usually mean is that they don't want the business consequences of duplicate processing. Those are not necessarily the same thing.

Exactly-once semantics are expensive and often misunderstood. They typically provide exactly-once processing within a transactional boundary, not exactly-once business outcomes. For example, they generally cannot guarantee that a customer receives only one email, SMS, or webhook because those effects occur in external systems outside the transaction.

If someone insists on exactly-once delivery, ask what business problem they're trying to solve. More often than not, the real requirement is "Don't charge the customer twice" or "Don't create duplicate orders", not a strict messaging guarantee.

As a rule of thumb, start with at-least-once delivery and design for duplicates. Only pay the cost of exactly-once semantics when there is a demonstrated business requirement that justifies the additional complexity.
