using A2A;
using Microsoft.Agents.AI;

string agentUrl = Environment.GetEnvironmentVariable("A2A_AGENT_URL")!;

// Reads the agent card from /.well-known/agent-card.json
A2ACardResolver cardResolver = new(new Uri(agentUrl));

AgentCard agentCard = await cardResolver.GetAgentCardAsync();

Console.WriteLine($"[A2A] Found '{agentCard.Name}': {agentCard.Description}");

// The remote agent becomes an AIAgent, called like any local one
AIAgent agent = agentCard.AsAIAgent();

// One session, so the follow-up continues the same conversation on the remote side
AgentSession session = await agent.CreateSessionAsync();

string[] userInputs =
[
    "What should I do this weekend in Auckland?",
    "What about the weekend after that?"
];

foreach (string userInput in userInputs)
{
    Console.WriteLine();
    Console.WriteLine("--- User ---");
    Console.WriteLine(userInput);

    AgentResponse response = await agent.RunAsync(userInput, session);

    Console.WriteLine();
    Console.WriteLine("--- Agent Response ---");
    Console.WriteLine(response.ToString());
}

Console.ReadLine();