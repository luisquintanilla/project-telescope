var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.ChatWithTelescope>("chat-with-telescope");

builder.Build().Run();
