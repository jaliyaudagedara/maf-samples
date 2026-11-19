namespace MafSamples.DurableWorkflow.Constants;

internal static class Agents
{
    // No hyphens: a workflow resolves its agents by executor id, where a hyphen becomes an underscore
    public const string WeatherAssessor = "WeatherAssessor";

    public const string ItineraryPlanner = "ItineraryPlanner";
}
