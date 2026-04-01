using System.ClientModel;
using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;

// --- Build host with Aspire service defaults + Telescope client ---
var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

// Register Telescope client from Aspire connection string (or default pipe name standalone)
builder.AddTelescopeClient("telescope", settings =>
{
    settings.AgentId = "chat-with-telescope-sample";
    settings.AgentName = "Chat With Telescope Sample";
    settings.AgentVersion = "0.1.0";
    settings.EnableSensitiveData = true;
    settings.MaxConnectRetries = 2;
    settings.ConnectTimeout = TimeSpan.FromSeconds(2);
});

// --- Configuration (user-secrets + env vars already handled by Host builder) ---
var token = builder.Configuration["GitHub:Token"] ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
if (string.IsNullOrEmpty(token))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("ERROR: No GitHub token found.");
    Console.WriteLine();
    Console.WriteLine("Option 1 — Aspire parameter (when running via AppHost):");
    Console.WriteLine("  dotnet user-secrets --project AppHost set \"Parameters:github-token\" \"ghp_your_token\"");
    Console.WriteLine("  (Or enter it in the Aspire Dashboard when prompted)");
    Console.WriteLine();
    Console.WriteLine("Option 2 — user-secrets (standalone):");
    Console.WriteLine("  dotnet user-secrets --project ChatWithTelescope set \"GitHub:Token\" \"ghp_your_token\"");
    Console.WriteLine();
    Console.WriteLine("Option 3 — environment variable:");
    Console.WriteLine("  set GITHUB_TOKEN=ghp_your_token");
    Console.ResetColor();
    return;
}
var model = builder.Configuration["GitHub:Model"] ?? Environment.GetEnvironmentVariable("GITHUB_MODEL") ?? "openai/gpt-4o-mini";

// Build and start host — this initializes the OTEL pipeline and exporters
using var host = builder.Build();
await host.StartAsync();

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

// --- Build the M.E.AI pipeline using DI-resolved Telescope settings ---
Console.WriteLine("Connecting to Telescope service...");
Console.WriteLine("(Aspire manages Telescope, or run 'tele service start' standalone)");
Console.WriteLine();

using var pipeline = innerClient
    .AsBuilder()
    .UseTelescope(host.Services)
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

// Graceful shutdown — flushes pending OTLP batches to Aspire Dashboard
await host.StopAsync();

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
