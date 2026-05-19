using MafSamples.DurableOrchestrator.Constants;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DurableTask;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

public static class WeekendPlanOrchestrations
{
    [Function(nameof(WeekendPlanOrchestration))]
    public static async Task<string> WeekendPlanOrchestration(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        WeekendRequest request = context.GetInput<WeekendRequest>()!;

        // 1. Ask the weather-assessor whether the weather is suitable for outdoor activities
        DurableAIAgent weatherAgent = context.GetAgent(Agents.WeatherAssessor);
        AgentSession weatherSession = await weatherAgent.CreateSessionAsync();

        AgentResponse<WeatherAssessment> weatherResponse = await weatherAgent.RunAsync<WeatherAssessment>(
            message: $"Is the weather in {request.City} suitable for outdoor activities this weekend?",
            session: weatherSession);

        WeatherAssessment assessment = weatherResponse.Result;

        if (!assessment.IsSuitableForOutdoors)
        {
            return await context.CallActivityAsync<string>(nameof(NotifyIndoorPlan), assessment.Reason);
        }

        // 2. Ask the itinerary-planner to build a plan
        DurableAIAgent plannerAgent = context.GetAgent(Agents.ItineraryPlanner);
        AgentSession plannerSession = await plannerAgent.CreateSessionAsync();

        AgentResponse<WeekendItinerary> plannerResponse = await plannerAgent.RunAsync<WeekendItinerary>(
            message: $"Create a weekend itinerary for {request.City}",
            session: plannerSession);

        return plannerResponse.Result.Summary;
    }

    [Function(nameof(NotifyIndoorPlan))]
    public static string NotifyIndoorPlan([ActivityTrigger] string reason)
    {
        Console.WriteLine($"[Activity] Suggesting indoor plan. Reason: {reason}");
        return $"Bad weather for outdoor activities ({reason}). Try indoor options like museums or movies.";
    }
}

// Structured outputs requested from the two agents inside the orchestration
public record WeatherAssessment(bool IsSuitableForOutdoors, string Reason);

public record WeekendItinerary(string Summary);
