using Azure.AI.Projects;
using Azure.Identity;
using MafSamples.DurableWorkflow.Constants;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AzureFunctions;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;

var endpoint = Environment.GetEnvironmentVariable("AZURE_AI_ENDPOINT")!;
var deploymentName = Environment.GetEnvironmentVariable("AZURE_AI_MODEL")!;

// For local development, using AzureCliCredential
var credential = new AzureCliCredential();
var projectClient = new AIProjectClient(new Uri(endpoint), credential);

// Agent 1: assesses whether the weather is suitable for outdoor activities
AIAgent weatherAssessor = projectClient
    .AsAIAgent(
        model: deploymentName,
        name: Agents.WeatherAssessor,
        instructions: """
            You are given a city. Determine whether the weather there is suitable for outdoor leisure activities this weekend.
            Use the available tools to check the weather and current date.
            Respond with the city, the weather, and whether it is suitable for outdoor activities.
            """,
        tools: [
            AIFunctionFactory.Create(Tools.GetWeather),
            AIFunctionFactory.Create(Tools.GetCurrentDate)
        ]);

// Agent 2: builds the weekend itinerary, based on the weather assessment
AIAgent itineraryPlanner = projectClient
    .AsAIAgent(
        model: deploymentName,
        name: Agents.ItineraryPlanner,
        instructions: """
            You are given a weather assessment for a city. Build a weekend itinerary using the available activities for that city.
            If the weather is not suitable for outdoor activities, only include indoor activities.
            Include the date of the weekend in the itinerary.
            """,
        tools: [
            AIFunctionFactory.Create(Tools.GetActivities),
            AIFunctionFactory.Create(Tools.GetCurrentDate)
        ]);

// WeatherAssessor -> ItineraryPlanner, the output of one is the input of the next
Workflow workflow = new WorkflowBuilder(weatherAssessor)
    .WithName("WeekendPlan")
    .AddEdge(weatherAssessor, itineraryPlanner)
    .Build();

using IHost app = FunctionsApplication
    .CreateBuilder(args)
    .ConfigureFunctionsWebApplication()
    .ConfigureDurableAgents(options =>
    {
        // The workflow registers its agents itself; registering them here too lets us set the history owner.
        // Foundry keeps the conversation, so the durable session needs a stable key for that owner
        options.AddAIAgent(weatherAssessor, timeToLive: null, configureHistory: h => h.ProviderKey = new("foundry.v1"));
        options.AddAIAgent(itineraryPlanner, timeToLive: null, configureHistory: h => h.ProviderKey = new("foundry.v1"));
    })
    .ConfigureDurableWorkflows(options => options.AddWorkflow(workflow, enableStatusEndpoint: true))
    .Build();

app.Run();
