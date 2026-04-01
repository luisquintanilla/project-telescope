using Telescope.Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var githubToken = builder.AddParameter("github-token", secret: true);

// Telescope service — managed by Aspire (starts 'tele service start --foreground')
var telescope = builder.AddTelescope("telescope");

// Telescope Dashboard — optional, opens the Telescope UI
builder.AddTelescopeDashboard();

builder.AddProject<Projects.ChatWithTelescope>("chat-with-telescope")
    .WithEnvironment("GitHub__Token", githubToken)
    .WithReference(telescope);

builder.Build().Run();
