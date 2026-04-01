using System.ClientModel;
using System.ComponentModel;
using System.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using Telescope.Extensions.AI.ChatCompletion;

// --- Configuration ---
var config = new ConfigurationBuilder()
    .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
    .Build();

var token = config["GitHub:Token"] ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
if (string.IsNullOrEmpty(token))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("ERROR: No GitHub token found.");
    Console.WriteLine("  dotnet user-secrets --project BuggyAgent set \"GitHub:Token\" \"ghp_your_token\"");
    Console.ResetColor();
    return;
}
var model = config["GitHub:Model"] ?? Environment.GetEnvironmentVariable("GITHUB_MODEL") ?? "openai/gpt-4o-mini";

Console.WriteLine("🐛 Buggy Agent — Telescope Skill Demo");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine($"  Model: {model}");
Console.WriteLine();

// --- Create GitHub Models client ---
var client = new OpenAIClient(
    credential: new ApiKeyCredential(token),
    options: new OpenAIClientOptions { Endpoint = new Uri("https://models.github.ai/inference") });
var innerClient = client.GetChatClient(model).AsIChatClient();

// --- Build the M.E.AI pipeline with Telescope ---
using var pipeline = innerClient
    .AsBuilder()
    .UseTelescope(options =>
    {
        options.AgentId = "buggy-agent-demo";
        options.AgentName = "Buggy Agent Demo";
        options.AgentVersion = "0.1.0";
        options.EnableSensitiveData = true;
        options.Transport.MaxConnectRetries = 2;
        options.Transport.ConnectTimeout = TimeSpan.FromSeconds(2);
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

// --- Ask about Seattle weather (this will hit the bug!) ---
Console.WriteLine("Asking: What's the weather in Seattle and what time is it in Pacific time?");
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
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("⚠️  Notice: The weather result looks wrong!");
Console.WriteLine("   Use 'tele sessions list' and 'tele turns list <session-id>' to investigate.");
Console.WriteLine("   The Telescope skill can help your coding agent find and fix the bug.");
Console.ResetColor();

// --- Tool implementations (one has a bug!) ---
[Description("Gets the current weather for a given location")]
static string GetCurrentWeather([Description("The city and state, e.g. San Francisco, CA")] string location)
{
    // BUG: "seatle" is misspelled — should be "seattle"
    // This causes the tool to return the fallback "Weather data not available"
    // instead of the actual weather for Seattle.
    // The Telescope trace will show ToolCallCompleted with the wrong result.
    return location.ToLowerInvariant() switch
    {
        var l when l.Contains("seatle") => "72°F, partly cloudy with a chance of rain",
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
