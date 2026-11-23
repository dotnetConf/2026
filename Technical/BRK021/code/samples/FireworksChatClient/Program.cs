using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using OpenAI.Responses;
using System.ClientModel.Primitives;

IConfiguration config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

string endpoint = config["AzureOpenAI:Endpoint"]
    ?? throw new InvalidOperationException("Set AzureOpenAI:Endpoint with 'dotnet user-secrets set'. See README.md.");
string deployment = config["AzureOpenAI:Deployment"] ?? "gpt-6-luna";
string mcpEndpoint = config["Fireworks:McpEndpoint"] ?? "http://127.0.0.1:5399/mcp";

await using McpClient mcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new() { Endpoint = new Uri(mcpEndpoint), Name = "fireworks-show-control" }));

// Keyless auth: uses your 'az login' identity, which needs the "Cognitive Services OpenAI User" role.
// GPT-6 Luna only supports function tools through the Responses API, not Chat Completions.
IChatClient modelClient = new ResponsesClient(
        new BearerTokenPolicy(new DefaultAzureCredential(), "https://cognitiveservices.azure.com/.default"),
        new ResponsesClientOptions { Endpoint = new Uri(new Uri(endpoint), "openai/v1/") })
    .AsIChatClient(deployment);

var tools = await mcpClient.ListToolsAsync();

IChatClient chat =
    new ChatClientBuilder(modelClient)
        .UseFunctionInvocation()
        .Build();

var response = await chat.GetResponseAsync(
    "Launch a .NET Conf fireworks show!",
    new ChatOptions { Tools = [.. tools] });

Console.WriteLine(response.Text);
