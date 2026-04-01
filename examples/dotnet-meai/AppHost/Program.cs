var builder = DistributedApplication.CreateBuilder(args);

var githubToken = builder.AddParameter("github-token", secret: true);

builder.AddProject<Projects.ChatWithTelescope>("chat-with-telescope")
    .WithEnvironment("GitHub__Token", githubToken);

builder.Build().Run();
