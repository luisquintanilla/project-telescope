using System.ClientModel;
using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;
using Telescope.Extensions.AI.ChatCompletion;

// --- Build host with Aspire service defaults ---
var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

// --- Configuration (user-secrets + env vars already handled by Host builder) ---
var token = builder.Configuration["GitHub:Token"] ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
if (string.IsNullOrEmpty(token))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("ERROR: No GitHub token found.");
    Console.WriteLine();
    Console.WriteLine("Option 1 — user-secrets (recommended):");
    Console.WriteLine("  dotnet user-secrets set \"GitHub:Token\" \"ghp_your_token\"");
    Console.WriteLine();
    Console.WriteLine("Option 2 — environment variable:");
    Console.WriteLine("  set GITHUB_TOKEN=ghp_your_token");
    Console.ResetColor();
    return;
}
var model = builder.Configuration["GitHub:Model"] ?? Environment.GetEnvironmentVariable("GITHUB_MODEL") ?? "openai/gpt-4o-mini";

Console.WriteLine("🔭 Chat With Telescope Sample");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine($"  Provider: GitHub Models");
Console.WriteLine($"  Model:    {model}");
Console.WriteLine();

// --- Create GitHub Models client ---
var client = new OpenAIClient(
    credential: new ApiKeyCredential(token),
    options: new OpenAIClientOptions { Endpoint = new Uri("https://models.github.ai/inference") });
var innerClient = client.GetChatClient(model).AsIChatClient();

// --- Build the M.E.AI pipeline ---
Console.WriteLine("Connecting to Telescope service...");
Console.WriteLine("(Run 'tele service start' first, or events will be silently skipped)");
Console.WriteLine();

using var pipeline = innerClient
    .AsBuilder()
    .UseTelescope(options =>
    {
        options.AgentId = "chat-with-telescope-sample";
        options.AgentName = "Chat With Telescope Sample";
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
Console.WriteLine("Run 'tele sessions list' or open 'telescope-dashboard' to see the captured events!");

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
