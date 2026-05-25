using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AzureFunctions;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using System.ComponentModel;

var endpoint = Environment.GetEnvironmentVariable("AZURE_AI_ENDPOINT")!;
var deploymentName = Environment.GetEnvironmentVariable("AZURE_AI_MODEL")!;

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
        ]);

// Automatically creates HTTP endpoints and manages state persistence
using IHost app = FunctionsApplication
    .CreateBuilder(args)
    .ConfigureFunctionsWebApplication()
    .ConfigureDurableAgents(options =>
        options.AddAIAgent(agent)
    )
    .Build();

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