using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using System.ComponentModel;

string endpoint = Environment.GetEnvironmentVariable("AZURE_AI_ENDPOINT")!;
string deploymentName = Environment.GetEnvironmentVariable("AZURE_AI_MODEL")!;

// For local development, using AzureCliCredential
var credential = new AzureCliCredential();

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
        Never ask the user to confirm in chat, just call the booking tool.
        If the booking tool reports it was not approved, say it was not booked and don't try again.
        If the user asks about activities or the weather, hand off to the activities agent.
        """,
    tools: [
        AIFunctionFactory.Create(GetRestaurants, nameof(GetRestaurants)),
        // Booking a table is worth a human's confirmation, so the run pauses before it happens
        new ApprovalRequiredAIFunction(AIFunctionFactory.Create(BookTable, nameof(BookTable)))
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

// Run the agent, one session so the agents share the conversation
AgentSession session = await weekendConcierge.CreateSessionAsync();

string[] userInputs =
[
    "What should I do this weekend in Auckland?",
    "Where can we eat afterwards?",
    "Book the first one for 2 people at 7pm on Saturday."
];

foreach (string userInput in userInputs)
{
    Console.WriteLine();
    Console.WriteLine("--- User ---");
    Console.WriteLine(userInput);

    AgentResponse response = await weekendConcierge.RunAsync(userInput, session);

    // The agent stops and asks before running a tool that needs approval
    while (GetApprovalRequests(response) is { Count: > 0 } approvalRequests)
    {
        List<ChatMessage> approvals = [];
        Dictionary<string, bool> decisions = [];

        foreach (ToolApprovalRequestContent request in approvalRequests)
        {
            // The same tool call can be asked more than once, so decide once and reuse the answer
            if (!decisions.TryGetValue(request.ToolCall.CallId, out bool approved))
            {
                Console.WriteLine();
                Console.WriteLine("--- Approval needed ---");

                if (request.ToolCall is FunctionCallContent toolCall)
                {
                    Console.WriteLine($"{toolCall.Name}({string.Join(", ", toolCall.Arguments?.Select(a => $"{a.Key}: {a.Value}") ?? [])})");
                }

                Console.Write("Approve? (y/n): ");

                approved = Console.ReadLine()?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true;
                decisions[request.ToolCall.CallId] = approved;
            }

            // CreateResponse correlates the answer with the request for us
            approvals.Add(new ChatMessage(ChatRole.User, [request.CreateResponse(approved)]));
        }

        response = await weekendConcierge.RunAsync(approvals, session);
    }

    Console.WriteLine();
    Console.WriteLine("--- Agent Response ---");
    Console.WriteLine(response.ToString());

    foreach (ErrorContent error in response.Messages.SelectMany(m => m.Contents).OfType<ErrorContent>())
    {
        Console.WriteLine($"[Error] {error.Message}");
    }
}

static List<ToolApprovalRequestContent> GetApprovalRequests(AgentResponse response) =>
    [.. response.Messages
    .SelectMany(message => message.Contents)
    .OfType<ToolApprovalRequestContent>()];

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
