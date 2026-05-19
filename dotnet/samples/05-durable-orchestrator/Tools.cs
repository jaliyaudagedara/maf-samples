using System.ComponentModel;

public static class Tools
{
    [Description("Returns weather data for a given city.")]
    public static WeatherResult GetWeather(
        [Description("The city to get the weather for.")] string city)
    {
        Console.WriteLine($"[Tool] Getting weather for '{city}'.");

        return new WeatherResult(18, "Sunny");
    }

    [Description("Returns a list of leisure activities for a given city and date, each with a name and location.")]
    public static List<LeisureActivity> GetActivities(
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
    public static string GetCurrentDate()
    {
        Console.WriteLine("[Tool] Getting current date");

        return DateTime.Now.ToString("yyyy-MM-dd");
    }
}

public record WeatherResult(int Temperature, string Description);

public record LeisureActivity(string Name, string Location);
