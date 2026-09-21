using Azure.AI.AgentServer.Core;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using System.ComponentModel;

// Foundry injects these when the agent is hosted, locally they come from launchSettings.json
string endpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")!;
string deploymentName = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME")!;

// Locally this is the developer, in Foundry it is the agent's own managed identity
var credential = new DefaultAzureCredential();

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
            AIFunctionFactory.Create(GetWeather, nameof(GetWeather)),
            AIFunctionFactory.Create(GetActivities, nameof(GetActivities)),
            AIFunctionFactory.Create(GetCurrentDate, nameof(GetCurrentDate))
        ]);

// Serve the agent over the Responses protocol, on port 8088 by default
var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
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
