using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Runtime.CompilerServices;

string endpoint = Environment.GetEnvironmentVariable("AZURE_AI_ENDPOINT")!;
string deploymentName = Environment.GetEnvironmentVariable("AZURE_AI_MODEL")!;

// For local development, using AzureCliCredential
var credential = new AzureCliCredential();

AIAgent agent = new AIProjectClient(new Uri(endpoint), credential)
    .AsAIAgent(
        model: deploymentName,
        name: "weekend-planner",
        instructions: """
            You help users plan their weekends and choose the best activities for the given weather.
            If an activity would be unpleasant in weather, don't suggest it.
            Include date of the weekend in response.
            """,
        tools: [
            AIFunctionFactory.Create(GetWeather),
            AIFunctionFactory.Create(GetActivities),
            AIFunctionFactory.Create(GetCurrentDate)
        ],
        clientFactory: (chatClient) => chatClient
            .AsBuilder()
            .Use(getResponseFunc: CustomChatClientMiddleware, getStreamingResponseFunc: CustomChatClientStreamlingMiddleware)
            .Build())
    .AsBuilder()
    .Use(runFunc: CustomAgentRunMiddleware, runStreamingFunc: CustomAgentRunStreamingMiddleware)
    .Use(CustomFunctionCallingMiddleware)
    .Build();

// Run the agent
string userInput = "What should I do this weekend in Auckland?";
AgentResponse response = await agent.RunAsync(userInput);

Console.WriteLine();
Console.WriteLine("--- Agent Response ---");
Console.WriteLine(response.ToString());

// ============================================================================
// Tools
// ============================================================================

[Description("Returns weather data for a given city.")]
static WeatherResult GetWeather(
    [Description("The city to get the weather for.")] string city)
{
    Console.WriteLine($"         [Tool] Calling '{nameof(GetWeather)}' for '{city}'.");

    return new WeatherResult(18, "Rainy");
}

[Description("Returns a list of leisure activities for a given city and date, each with a name and location.")]
static List<LeisureActivity> GetActivities(
    [Description("The city to get activities for.")] string city,
    [Description("The date to get activities for in format YYYY-MM-DD.")] string date)
{
    Console.WriteLine($"         [Tool] Calling '{nameof(GetActivities)}' for '{city}' on '{date}'.");

    return
    [
        new("Hiking", city),
        new("Beach", city),
        new("Museum", city)
    ];
}

[Description("Gets the current date from the system and returns as a string in format YYYY-MM-DD.")]
static string GetCurrentDate()
{
    Console.WriteLine("         [Tool] Getting current date");

    return DateTime.Now.ToString("yyyy-MM-dd");
}

// ============================================================================
// Agent Run Middleware
// ============================================================================

async Task<AgentResponse> CustomAgentRunMiddleware(IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent agent,
    CancellationToken cancellationToken)
{
    Console.WriteLine($"[AgentRun] Message Count: '{messages.Count()}'.");

    AgentResponse response = await agent.RunAsync(messages, session, options, cancellationToken)
        .ConfigureAwait(false);

    Console.WriteLine($"[AgentRun] Response Message Count: '{response.Messages.Count}'.");

    return response;
}

async IAsyncEnumerable<AgentResponseUpdate> CustomAgentRunStreamingMiddleware(IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent agent,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    Console.WriteLine($"[AgentRunStreaming] Message Count: '{messages.Count()}'.");

    List<AgentResponseUpdate> updates = [];
    await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(messages, session, options, cancellationToken))
    {
        updates.Add(update);
        yield return update;
    }

    Console.WriteLine($"[AgentRunStreaming] Response Message Count: '{updates.ToAgentResponse().Messages.Count}'.");
}

// ============================================================================
// Function Calling Middleware
// ============================================================================

async ValueTask<object?> CustomFunctionCallingMiddleware(
    AIAgent agent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
{
    Console.WriteLine($"      [FunctionCall] Calling: '{context!.Function.Name}'.");

    object? result = await next(context, cancellationToken);

    Console.WriteLine($"      [FunctionCall] Result: '{context!.Function.Name}' = '<OMITTED>'.");

    return result;
}

// ============================================================================
// Chat Client Middleware
// ============================================================================

async Task<ChatResponse> CustomChatClientMiddleware(IEnumerable<ChatMessage> messages,
    ChatOptions? options,
    IChatClient client,
    CancellationToken token)
{
    LogChatClientMessages("Messages", messages);

    ChatResponse response = await client.GetResponseAsync(messages, options, token)
        .ConfigureAwait(false);

    LogChatClientMessages("Response", response.Messages);

    return response;
}

async IAsyncEnumerable<ChatResponseUpdate> CustomChatClientStreamlingMiddleware(IEnumerable<ChatMessage> messages,
    ChatOptions? options,
    IChatClient client,
    [EnumeratorCancellation] CancellationToken token)
{
    LogChatClientMessages("Messages", messages);

    List<ChatResponseUpdate> updates = [];
    await foreach (ChatResponseUpdate update in client.GetStreamingResponseAsync(messages, options, token))
    {
        updates.Add(update);
        yield return update;
    }

    LogChatClientMessages("Response", updates.ToChatResponse().Messages);
}

void LogChatClientMessages(string label, IEnumerable<ChatMessage> messages)
{
    Console.WriteLine();
    Console.WriteLine($"   [ChatClient] {label}:");
    foreach (ChatMessage message in messages)
    {
        foreach (AIContent content in message.Contents)
        {
            string detail = content switch
            {
                TextContent text => text.Text,
                FunctionCallContent fc => $"Call: {fc.Name}({string.Join(", ", fc.Arguments?.Select(a => $"{a.Key}={a.Value}") ?? [])})",
                FunctionResultContent fr => $"Result: {fr.CallId} = '<OMITTED>'",
                _ => content.GetType().Name
            };
            Console.WriteLine($"      [{message.Role}] {detail}");
        }
    }
}

// ============================================================================
// Types
// ============================================================================

record WeatherResult(int Temperature, string Description);

record LeisureActivity(string Name, string Location);
