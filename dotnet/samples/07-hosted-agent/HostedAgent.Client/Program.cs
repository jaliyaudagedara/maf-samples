using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using OpenAI.Responses;

// ProjectResponsesClient, used to call the agent, is still experimental
#pragma warning disable OPENAI001

string endpoint = Environment.GetEnvironmentVariable("AZURE_AI_PROJECT_ENDPOINT")!;
const string agentName = "weekend-planner";

// For local development, using AzureCliCredential
var credential = new AzureCliCredential();

AIProjectClient projectClient = new(new Uri(endpoint), credential);

// A hosted agent is called through its own endpoint, not the project one
ProjectResponsesClient responsesClient = projectClient.ProjectOpenAIClient
    .GetProjectResponsesClientForAgentEndpoint(agentName);

string userInput = "What should I do this weekend in Auckland?";

Console.WriteLine("--- User ---");
Console.WriteLine(userInput);

ResponseResult response = responsesClient.CreateResponse(userInput);

Console.WriteLine();
Console.WriteLine("--- Agent Response ---");
Console.WriteLine(response.GetOutputText());
