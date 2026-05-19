using Azure.AI.Projects;
using Azure.Identity;
using MafSamples.DurableOrchestrator.Constants;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DurableTask;
using Microsoft.Agents.AI.Hosting.AzureFunctions;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;

var endpoint = Environment.GetEnvironmentVariable("AZURE_AI_ENDPOINT")!;
var deploymentName = Environment.GetEnvironmentVariable("AZURE_AI_MODEL")!;

// For local development, using AzureCliCredential
var credential = new AzureCliCredential();
var projectClient = new AIProjectClient(new Uri(endpoint), credential);

// Agent 1 — assesses whether the weather is suitable for outdoor activities
AIAgent weatherAssessor = projectClient
    .AsAIAgent(
        model: deploymentName,
        name: Agents.WeatherAssessor,
        instructions: """
            You determine whether the weather in a given city is suitable for outdoor leisure activities this weekend.
            Use the available tools to check the weather and current date.
            Respond with a structured result: IsSuitableForOutdoors and a short Reason.
            """,
        tools: [
            AIFunctionFactory.Create(Tools.GetWeather),
            AIFunctionFactory.Create(Tools.GetCurrentDate)
        ]);

// Agent 2 — builds the weekend itinerary
AIAgent itineraryPlanner = projectClient
    .AsAIAgent(
        model: deploymentName,
        name: Agents.ItineraryPlanner,
        instructions: """
            You build a weekend itinerary using the available activities for a given city.
            Include the date of the weekend in the itinerary.
            Respond with a structured result containing a Summary string.
            """,
        tools: [
            AIFunctionFactory.Create(Tools.GetActivities),
            AIFunctionFactory.Create(Tools.GetCurrentDate)
        ]);

// Register the agents so orchestrations can resolve them via context.GetAgent(name)
using IHost app = FunctionsApplication
    .CreateBuilder(args)
    .ConfigureFunctionsWebApplication()
    .ConfigureDurableAgents(options =>
    {
        options.AddAIAgent(weatherAssessor);
        options.AddAIAgent(itineraryPlanner);
    })
    .Build();

app.Run();
