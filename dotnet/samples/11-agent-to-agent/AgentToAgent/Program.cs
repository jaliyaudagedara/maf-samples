using A2A;
using A2A.AspNetCore;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using System.ComponentModel;

// AgentSessionStore, used to keep the conversation between A2A messages, is still experimental
#pragma warning disable MAAI001

string endpoint = Environment.GetEnvironmentVariable("AZURE_AI_ENDPOINT")!;
string deploymentName = Environment.GetEnvironmentVariable("AZURE_AI_MODEL")!;
const string agentUrl = "http://localhost:5000";

// For local development, using AzureCliCredential
var credential = new AzureCliCredential();

AIAgent agent = new AIProjectClient(new Uri(endpoint), credential)
    .AsAIAgent(
        model: deploymentName,
        name: "weekend-planner",
        description: "Plans weekends and suggests activities based on the weather.",
        instructions: """
            You help users plan their weekends and choose the best activities for the given weather.
            If an activity would be unpleasant in weather, don't suggest it.
            Include date of the weekend in response.
            """,
        tools: [
            AIFunctionFactory.Create(GetWeather, nameof(GetWeather)),
            AIFunctionFactory.Create(GetActivities, nameof(GetActivities)),
            AIFunctionFactory.Create(GetCurrentDate, nameof(GetCurrentDate))
        ]);

// What other agents read to discover this one, before they send it anything
AgentCard agentCard = new()
{
    Name = agent.Name!,
    Description = agent.Description!,
    Version = "1.0.0",
    DefaultInputModes = ["text/plain"],
    DefaultOutputModes = ["text/plain"],
    Capabilities = new()
    {
        Streaming = false,
        PushNotifications = false
    },
    Skills =
    [
        new()
        {
            Id = "weekend-planning",
            Name = "Weekend planning",
            Description = "Suggests weekend activities for a city, based on the weather.",
            Examples = ["What should I do this weekend in Auckland?"]
        }
    ],
    SupportedInterfaces =
    [
        new()
        {
            Url = agentUrl,
            ProtocolBinding = ProtocolBindingNames.JsonRpc,
            ProtocolVersion = "1.0"
        }
    ]
};

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddA2AServer(agent);

// Sessions are not kept by default, so every message would start a new conversation
builder.Services
    .AddKeyedSingleton<AgentSessionStore>(agent.Name, new InMemoryAgentSessionStore());

WebApplication app = builder.Build();

app.MapA2AJsonRpc(agent, "/");

// Served at /.well-known/agent-card.json
app.MapWellKnownAgentCard(agentCard);

app.Run();

// ============================================================================
// Tools
// ============================================================================

[Description("Returns weather data for a given city.")]
static WeatherResult GetWeather(
    [Description("The city to get the weather for.")] string city)
{
    Console.WriteLine($"[Tool] Getting weather for '{city}'.");

    return new WeatherResult(18, "Rainy");
}

[Description("Returns a list of leisure activities for a given city and date, each with a name and location.")]
static List<LeisureActivity> GetActivities(
    [Description("The city to get activities for.")] string city,
    [Description("The date to get activities for in format YYYY-MM-DD.")] string date)
{
    Console.WriteLine($"[Tool] Getting activities for '{city}' on '{date}'.");

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
    Console.WriteLine("[Tool] Getting current date");

    return DateTime.Now.ToString("yyyy-MM-dd");
}

// ============================================================================
// Types
// ============================================================================

record WeatherResult(int Temperature, string Description);

record LeisureActivity(string Name, string Location);
