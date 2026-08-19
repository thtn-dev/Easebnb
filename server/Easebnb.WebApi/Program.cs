using System.IdentityModel.Tokens.Jwt;
using BuildingBlocks.Endpoints;
using BuildingBlocks.Infrastructure;
using BuildingBlocks.Infrastructure.DomainEvent;
using BuildingBlocks.Infrastructure.ObjectStorage.S3;
using BuildingBlocks.SharedKernel;
using DotNetEnv;
using Scalar.AspNetCore;
using Easebnb.Database;
using Easebnb.Identity.Infrastructure;
using Easebnb.Identity.Infrastructure.Database;
using Easebnb.WebApi;
using Easebnb.WebApi.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
Env.Load();
var builder = WebApplication.CreateBuilder(args);

var uploadSettings = builder.Configuration.GetSection("UploadSettings").Get<UploadSettings>()
                     ?? new UploadSettings();
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = uploadSettings.GlobalMaxBodySizeBytes;
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = uploadSettings.GlobalMaxBodySizeBytes;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
 
    options.AddFixedWindowLimiter("file-upload", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
});
builder.Services.Configure<AvatarUploadSettings>(
    builder.Configuration.GetSection("UploadSettings:Avatar"));
builder.Services.ConfigureOpenApi();
builder.AddServiceDefaults();



#region Domain Event
{
    builder.Services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
    builder.Services.AddScoped<IDomainEventsAccessor, DomainEventsAccessor<AppIdentityDbContext>>();
}
#endregion

#region Identity Module
{
    builder.Services.AddIdentityModule(builder.Configuration);
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

builder.Services.AddS3ObjectStorage(builder.Configuration);

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
app.UseRateLimiter();
app.MapDefaultEndpoints();
app.MapEndpoints();
app.Run();