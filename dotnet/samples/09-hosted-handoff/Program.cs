using Azure.AI.AgentServer.Core;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using System.ComponentModel;

// Foundry injects these when the agent is hosted, locally they come from launchSettings.json
string endpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")!;
string deploymentName = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME")!;

// Locally this is the developer, in Foundry it is the agent's own managed identity
var credential = new DefaultAzureCredential();

AIProjectClient projectClient = new(new Uri(endpoint), credential);

AIAgent activitiesAgent = projectClient.AsAIAgent(
    model: deploymentName,
    name: "activities",
    description: "Plans what to do on the weekend, based on the weather.",
    instructions: """
        You help users plan their weekends and choose the best activities for the given weather.
        If an activity would be unpleasant in weather, don't suggest it.
        Include date of the weekend in response.
        If the user asks about food, restaurants or a table, hand off to the dining agent.
        """,
    tools: [
        AIFunctionFactory.Create(GetWeather, nameof(GetWeather)),
        AIFunctionFactory.Create(GetActivities, nameof(GetActivities)),
        AIFunctionFactory.Create(GetCurrentDate, nameof(GetCurrentDate))
    ]);

AIAgent diningAgent = projectClient.AsAIAgent(
    model: deploymentName,
    name: "dining",
    description: "Finds restaurants and books tables.",
    instructions: """
        You help users find a restaurant and book a table.
        Always look up restaurants before suggesting one, and never invent a restaurant.
        Confirm the restaurant, date, time and number of people before booking.
        If the user asks about activities or the weather, hand off to the activities agent.
        """,
    tools: [
        AIFunctionFactory.Create(GetRestaurants, nameof(GetRestaurants)),
        AIFunctionFactory.Create(BookTable, nameof(BookTable))
    ]);

// The model decides who answers next, not us
Workflow workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(activitiesAgent)
    .WithHandoffs(activitiesAgent, [diningAgent])
    .WithHandoffs(diningAgent, [activitiesAgent])
    // Follow-ups stay with the agent already helping, instead of going back to the first one
    .EnableReturnToPrevious()
    .Build();

// The whole workflow becomes a single agent
AIAgent weekendConcierge = workflow.AsAIAgent(
    name: "weekend-concierge",
    description: "Plans a weekend, and hands off between activities and dining.");

// Serve the whole workflow as one agent, over the Responses protocol
AgentHostBuilder builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(weekendConcierge);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

AgentHostApp app = builder.Build();
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

[Description("Returns a list of restaurants for a given city, each with a name and cuisine.")]
static List<Restaurant> GetRestaurants(
    [Description("The city to get restaurants for.")] string city)
{
    Console.WriteLine($"[Tool] Getting restaurants for '{city}'.");

    return
    [
        new("The Domain Kitchen", "Modern New Zealand"),
        new("Ponsonby Pasta", "Italian"),
        new("Harbour Yum Cha", "Chinese")
    ];
}

[Description("Books a table at a restaurant and returns a booking reference.")]
static Booking BookTable(
    [Description("The name of the restaurant.")] string restaurant,
    [Description("The date of the booking in format YYYY-MM-DD.")] string date,
    [Description("The time of the booking in format HH:mm.")] string time,
    [Description("The number of people.")] int people)
{
    Console.WriteLine($"[Tool] Booking a table for {people} at '{restaurant}' on '{date}' at '{time}'.");

    return new Booking($"BK-{Random.Shared.Next(1000, 10000)}", restaurant, date, time, people);
}

// ============================================================================
// Types
// ============================================================================

record WeatherResult(int Temperature, string Description);

record LeisureActivity(string Name, string Location);

record Restaurant(string Name, string Cuisine);

record Booking(string Reference, string Restaurant, string Date, string Time, int People);
