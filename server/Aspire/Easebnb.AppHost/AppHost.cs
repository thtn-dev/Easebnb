using Projects;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Easebnb_WebApi>("web-api");
builder.Build().Run();