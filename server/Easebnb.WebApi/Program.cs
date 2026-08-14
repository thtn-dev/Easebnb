using System.IdentityModel.Tokens.Jwt;
using BuildingBlocks.Endpoints;
using BuildingBlocks.Infrastructure;
using BuildingBlocks.Infrastructure.DomainEvent;
using BuildingBlocks.SharedKernel;
using DotNetEnv;
using Scalar.AspNetCore;
using Easebnb.Database;
using Easebnb.Identity.Infrastructure;
using Easebnb.Identity.Infrastructure.Database;
using Easebnb.WebApi.Extensions;

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
Env.Load();
var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureOpenApi();
builder.AddServiceDefaults();

var databaseSection = builder.Configuration.GetSection(DatabaseSettings.SectionName);

#region Domain Event
{
    builder.Services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
    builder.Services.AddScoped<IDomainEventsAccessor, DomainEventsAccessor<AppIdentityDbContext>>();
}
#endregion

#region Identity Module
{
    builder.Services.AddOptions<DatabaseSettings>()
    .Bind(databaseSection)
    .ValidateDataAnnotations()
    .ValidateOnStart();

    builder.Services.AddDatabase<AppIdentityDbContext>("Identity");
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork<AppIdentityDbContext>>();

    builder.Services.AddAspNetIdentityServices(builder.Configuration);
    builder.Services.AddIdentityModule();
}
#endregion

builder.Services.AddMediatR(cfg => { 
    cfg.RegisterServicesFromAssembly(typeof(AppIdentityDbContext).Assembly); 
});
builder.Services.AddApi<Program>();
builder.Services.AddProblemDetails();
builder.Services.AddCors(options =>
{
    // Allow all origins, methods, and headers for development
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin();
        policy.AllowAnyMethod();
        policy.AllowAnyHeader();
    });
});

var app = builder.Build();
app.UseCors("AllowAll");
app.UseForwardedHeaders();
app.UseStatusCodePages();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/docs", options =>
    {
        options.Title = "Web API";
        options.Theme = ScalarTheme.Purple;

        // Configure authentication
        options.AddPreferredSecuritySchemes("Bearer")
            .AddHttpAuthentication("Bearer", auth => { auth.Token = ""; });

        var addresses = app.Configuration["ASPNETCORE_URLS"]
                        ?? app.Configuration["urls"];

        var serverUrls = addresses?.Split(';');

        if (serverUrls == null) return;
        foreach (var url in serverUrls)
            options.AddServer(new ScalarServer(url.Trim(), "Local Development"));
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();
app.MapEndpoints();
app.Run();