using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Responses;
using System.ComponentModel;

// ResponseTool, used to declare the agent's function tools, is still experimental
#pragma warning disable OPENAI001

// Agent administration lives under the project, so this needs the project endpoint,
// not the resource one the earlier samples use
string endpoint = Environment.GetEnvironmentVariable("AZURE_AI_PROJECT_ENDPOINT")!;
string deploymentName = Environment.GetEnvironmentVariable("AZURE_AI_MODEL")!;
const string agentName = "weekend-planner-prompt";

// For local development, using AzureCliCredential
var credential = new AzureCliCredential();

AIProjectClient projectClient = new(new Uri(endpoint), credential);

AIFunction[] tools =
[
    AIFunctionFactory.Create(GetWeather, nameof(GetWeather)),
    AIFunctionFactory.Create(GetActivities, nameof(GetActivities)),
    AIFunctionFactory.Create(GetCurrentDate, nameof(GetCurrentDate))
];

// Foundry stores the tool schemas, this app executes the tools
DeclarativeAgentDefinition definition = new(deploymentName)
{
    Instructions = """
        You help users plan their weekends and choose the best activities for the given weather.
        If an activity would be unpleasant in weather, don't suggest it.
        Include date of the weekend in response.
        """
};

foreach (AIFunction tool in tools)
{
    definition.Tools.Add(ResponseTool.CreateFunctionTool(
        tool.Name,
        BinaryData.FromString(tool.JsonSchema.GetRawText()),
        strictModeEnabled: false,
        functionDescription: tool.Description));
}

// Create the agent in Foundry, every run creates a new version of it
ProjectsAgentVersion agentVersion =
    await projectClient.AgentAdministrationClient.CreateAgentVersionAsync(
        agentName,
        new ProjectsAgentVersionCreationOptions(definition));

Console.WriteLine($"[Agent] Created '{agentVersion.Name}' version '{agentVersion.Version}'.");

string userInput = "What should I do this weekend in Auckland?";

// Run the version that was just created, the tools are passed in from here
AIAgent agent = projectClient.AsAIAgent(agentVersion, tools: [.. tools]);
AgentResponse response = await agent.RunAsync(userInput);

Console.WriteLine();
Console.WriteLine("--- Agent Response ---");
Console.WriteLine(response.ToString());

// Or resolve an existing agent by name and run its latest version, no instructions needed here
ProjectsAgentRecord agentRecord =
    await projectClient.AgentAdministrationClient.GetAgentAsync(agentName);
AIAgent latestAgent = projectClient.AsAIAgent(agentRecord, tools: [.. tools]);

Console.WriteLine();
Console.WriteLine($"[Agent] Running the latest version of '{agentRecord.Name}'.");

AgentResponse latestResponse = await latestAgent.RunAsync(userInput);

Console.WriteLine();
Console.WriteLine("--- Agent Response (latest version) ---");
Console.WriteLine(latestResponse.ToString());

// Delete the agent, remove this line while poking at it in the portal
await projectClient.AgentAdministrationClient.DeleteAgentAsync(agentName);

Console.WriteLine();
Console.WriteLine($"[Agent] Deleted '{agentName}'.");

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
