# Fireworks Chat Client

A minimal `IChatClient` app that drives the [Fireworks Server](../FireworksServer/README.md) without an IDE. It lists the server's MCP tools and passes them to GPT-6 Luna through `Microsoft.Extensions.AI`:

```csharp
var tools = await mcpClient.ListToolsAsync();

IChatClient chat =
    new ChatClientBuilder(modelClient)
        .UseFunctionInvocation()
        .Build();

var response = await chat.GetResponseAsync(
    "Launch a .NET Conf fireworks show!",
    new ChatOptions { Tools = [.. tools] });
```

Each `McpClientTool` is an `AIFunction`, so `UseFunctionInvocation()` calls `launch_fireworks` over MCP when the model asks for it.

Unlike the servers, this sample needs network access to Azure OpenAI.

## Azure setup

Sign in with the Azure CLI and choose your subscription:

```powershell
az login
az account set --subscription "<subscription name or id>"
```

Create an Azure AI Services (Foundry) resource if you don't already have one, then deploy `gpt-6-luna`:

```powershell
$rg = "<resource-group>"
$account = "<ai-services-account>"

az group create --name $rg --location eastus2
az cognitiveservices account create --name $account --resource-group $rg `
    --kind AIServices --sku S0 --location eastus2 --custom-domain $account

az cognitiveservices account deployment create --name $account --resource-group $rg `
    --deployment-name gpt-6-luna --model-name gpt-6-luna --model-version 2026-09-22 `
    --model-format OpenAI --sku-name GlobalStandard --sku-capacity 50
```

The sample uses keyless Microsoft Entra ID auth through `DefaultAzureCredential`, so no API key is stored. Grant your signed-in user data-plane access. The `Owner` role alone is not enough.

```powershell
$accountId = az cognitiveservices account show --name $account --resource-group $rg --query id -o tsv
az role assignment create --assignee-object-id (az ad signed-in-user show --query id -o tsv) `
    --assignee-principal-type User --role "Cognitive Services OpenAI User" --scope $accountId
```

Role assignments can take a few minutes to apply.

## User secrets

From this directory, store the endpoint and deployment name:

```powershell
$endpoint = az cognitiveservices account show --name $account --resource-group $rg `
    --query 'properties.endpoints.\"OpenAI Language Model Instance API\"' -o tsv

dotnet user-secrets set "AzureOpenAI:Endpoint" $endpoint
dotnet user-secrets set "AzureOpenAI:Deployment" "gpt-6-luna"
```

| Setting | Default | Purpose |
| --- | --- | --- |
| `AzureOpenAI:Endpoint` | none (required) | Resource endpoint, such as `https://<account>.openai.azure.com/` |
| `AzureOpenAI:Deployment` | `gpt-6-luna` | Model deployment name |
| `Fireworks:McpEndpoint` | `http://127.0.0.1:5399/mcp` | Fireworks Streamable HTTP endpoint |

Environment variables such as `AzureOpenAI__Endpoint` override user secrets.

## Run

Start the Fireworks server in HTTP mode and open <http://127.0.0.1:5399> for the audience display:

```powershell
dotnet run --project samples\FireworksServer\FireworksServer.csproj
```

In a second terminal at the repository root:

```powershell
dotnet run --project samples\FireworksChatClient\FireworksChatClient.csproj
```

The browser display launches the show, and the console prints the model's reply.

## Notes

- GPT-6 Luna on Azure rejects function tools over `/v1/chat/completions`, so the sample uses the OpenAI Responses API (`ResponsesClient.AsIChatClient`) against the resource's `/openai/v1/` endpoint. Those APIs are marked experimental, so the project suppresses `OPENAI001`.
- If you get a `401` or `403`, confirm `az account show` uses the subscription that owns the resource and that the role assignment has applied.
