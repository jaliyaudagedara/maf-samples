using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using System.Net;

public static class WeekendPlanStarter
{
    [Function(nameof(StartWeekendPlan))]
    public static async Task<HttpResponseData> StartWeekendPlan(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "weekend-plan")] HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        WeekendRequest? request = await req.ReadFromJsonAsync<WeekendRequest>();
        if (request is null || string.IsNullOrWhiteSpace(request.City))
        {
            HttpResponseData badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("Body must include a non-empty 'city'.");
            return badRequest;
        }

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(WeekendPlanOrchestrations.WeekendPlanOrchestration), request);

        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }
}

// API contract for the HTTP starter
public record WeekendRequest(string City);
