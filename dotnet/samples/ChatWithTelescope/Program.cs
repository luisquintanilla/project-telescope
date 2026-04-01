using System.ComponentModel;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Telescope.Extensions.AI.ChatCompletion;

// --- Configuration ---
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
if (string.IsNullOrEmpty(endpoint))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("ERROR: Set the AZURE_OPENAI_ENDPOINT environment variable.");
    Console.WriteLine("Example: set AZURE_OPENAI_ENDPOINT=https://my-resource.openai.azure.com/");
    Console.ResetColor();
    return;
}
var deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? "gpt-4o";

Console.WriteLine("🔭 Chat With Telescope Sample");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine($"  Endpoint:   {endpoint}");
Console.WriteLine($"  Deployment: {deployment}");
Console.WriteLine();

// --- Create Azure OpenAI client ---
var azureClient = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
var innerClient = azureClient.GetChatClient(deployment).AsIChatClient();

// --- Build the M.E.AI pipeline ---
Console.WriteLine("Connecting to Telescope service...");
Console.WriteLine("(Make sure MockTelescopeServer is running in another terminal!)");
Console.WriteLine();

using var pipeline = innerClient
    .AsBuilder()
    .UseTelescope(options =>
    {
        options.AgentId = "chat-with-telescope-sample";
        options.AgentName = "Chat With Telescope Sample";
        options.AgentVersion = "0.1.0";
        options.EnableSensitiveData = true;
    })
    .UseFunctionInvocation()
    .Build();

// --- Define tools ---
var chatOptions = new ChatOptions
{
    Tools =
    [
        AIFunctionFactory.Create(GetCurrentWeather),
        AIFunctionFactory.Create(GetCurrentTime),
    ],
};

// --- Send a message that triggers function calling ---
Console.WriteLine("Sending chat message...");
Console.WriteLine();

var response = await pipeline.GetResponseAsync(
    "What's the weather in Seattle and what time is it in Pacific time?",
    chatOptions);

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("━━━ Response ━━━");
Console.ResetColor();
Console.WriteLine(response.Text);
Console.WriteLine();

if (response.Usage is { } usage)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"Tokens — Input: {usage.InputTokenCount}, Output: {usage.OutputTokenCount}, Total: {usage.TotalTokenCount}");
    Console.ResetColor();
}

Console.WriteLine();
Console.WriteLine("Check the MockTelescopeServer terminal to see the captured events!");

// --- Tool implementations ---
[Description("Gets the current weather for a given location")]
static string GetCurrentWeather([Description("The city and state, e.g. San Francisco, CA")] string location)
{
    return location.ToLowerInvariant() switch
    {
        var l when l.Contains("seattle") => "72°F, partly cloudy with a chance of rain",
        var l when l.Contains("new york") => "85°F, sunny and humid",
        var l when l.Contains("san francisco") => "65°F, foggy",
        _ => $"Weather data not available for {location}",
    };
}

[Description("Gets the current time in a given timezone")]
static string GetCurrentTime([Description("The timezone, e.g. Pacific, Eastern, UTC")] string timezone)
{
    try
    {
        var tz = timezone.ToLowerInvariant() switch
        {
            "pacific" or "pst" or "pdt" => TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"),
            "eastern" or "est" or "edt" => TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"),
            "central" or "cst" or "cdt" => TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"),
            "mountain" or "mst" or "mdt" => TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time"),
            "utc" or "gmt" => TimeZoneInfo.Utc,
            _ => TimeZoneInfo.FindSystemTimeZoneById(timezone),
        };
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).ToString("f");
    }
    catch
    {
        return $"Unknown timezone: {timezone}";
    }
}
