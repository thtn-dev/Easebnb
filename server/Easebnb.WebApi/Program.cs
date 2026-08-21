using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using BuildingBlocks.Endpoints;
using BuildingBlocks.Infrastructure.DomainEvent;
using BuildingBlocks.Infrastructure.ObjectStorage.S3;
using BuildingBlocks.SharedKernel;
using DotNetEnv;
using Scalar.AspNetCore;
using Easebnb.Identity.Infrastructure;
using Easebnb.Identity.Infrastructure.Database;
using Easebnb.WebApi;
using Easebnb.WebApi.Extensions;
using Easebnb.WebApi.Modules.Identity.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using Serilog.Context;
using Serilog.Sinks.OpenTelemetry;

Serilog.Debugging.SelfLog.Enable(Console.Error);
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
Env.Load();

var builder = WebApplication.CreateBuilder(args);

var uploadSettings = builder.Configuration.GetSection("UploadSettings").Get<UploadSettings>()
                     ?? new UploadSettings();
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = uploadSettings.GlobalMaxBodySizeBytes;
});

builder.Host.UseSerilog((context, services, config) =>
{
    config
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();

    var otlpEndpoint = context.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

    if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        config.WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = otlpEndpoint;
            options.Protocol = OtlpProtocol.Grpc;
        });
});
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
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

builder.Services.AddMediatR(cfg => { cfg.RegisterServicesFromAssembly(typeof(AppIdentityDbContext).Assembly); });
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
app.UseExceptionHandler(o =>
{
    o.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
        if (exceptionFeature is null) return;

        var handler = context.RequestServices.GetRequiredService<GlobalExceptionHandler>();
        await handler.TryHandleAsync(context, exceptionFeature.Error, CancellationToken.None);
    });
});

app.Use(async (context, next) =>
{
    var traceId = Activity.Current?.TraceId.ToString()
                  ?? context.TraceIdentifier;

    using (LogContext.PushProperty("TraceId", traceId))
    {
        await next();
    }
});

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms | TraceId: {TraceId}";

    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("TraceId",
            Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier);
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent);
    };
});

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