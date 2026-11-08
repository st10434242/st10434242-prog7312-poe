using System.Security.Cryptography;
using SmartX.Api;
using SmartX.Api.Endpoints;
using SmartX.Api.Hubs;
using SmartX.Core;

var builder = WebApplication.CreateBuilder(args);

// Singleton because these are the gateway's memory for the lifetime of the process
builder.Services.AddSingleton<TelemetryStore>();
builder.Services.AddSingleton<SensorRegistry>();
builder.Services.AddSingleton<NodeStateTracker>();
builder.Services.AddSingleton<FaultInjector>();
builder.Services.AddSingleton<IngestSignal>();
builder.Services.AddSingleton<TelemetryQueue>();
builder.Services.AddSingleton<AlertQueue>();
builder.Services.AddSingleton<DeviceTable>();
builder.Services.AddSingleton<TimelineLog>();
builder.Services.AddSingleton<FaultSet>();
builder.Services.AddSingleton<CommandHistory>();
builder.Services.AddSingleton<CommandOutbox>();
builder.Services.AddSingleton<PatternLearner>();
builder.Services.AddSingleton<InsightEngine>();
builder.Services.AddSingleton<MeshNetwork>();
builder.Services.AddSingleton<MeshHealth>();
builder.Services.AddSingleton<TelemetryHistoryIndex>();

builder.Services.AddSingleton(serviceProvider =>
{
    string directory = builder.Configuration["Attachments:Directory"] ?? "uploads";
    string? configuredKey = builder.Configuration["Attachments:Key"];

    byte[] key;

    if (string.IsNullOrWhiteSpace(configuredKey))
    {
        // Generated so the project runs out of the box, but nothing uploaded survives a restart
        key = RandomNumberGenerator.GetBytes(32);

        serviceProvider.GetRequiredService<ILogger<Program>>().LogWarning(
            "No Attachments:Key configured, generating a temporary one. " +
            "Set it with: dotnet user-secrets set \"Attachments:Key\" \"<base64 32 bytes>\"");
    }
    else
    {
        key = Convert.FromBase64String(configuredKey);
    }

    return new AttachmentStore(directory, key);
});

builder.Services.AddHostedService<StalenessSweeper>();
builder.Services.AddHostedService<QueueProcessor>();
builder.Services.AddHostedService<InsightPublisher>();
builder.Services.AddHostedService<MeshPublisher>();
builder.Services.AddHostedService<HistorySeeder>();

builder.Services.AddSignalR();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

// Makes malformed requests come back as ProblemDetails instead of a raw exception dump
builder.Services.AddProblemDetails();

// AllowCredentials is required for SignalR, and rules that out with AllowAnyOrigin
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins("http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();

app.MapSensorEndpoints();
app.MapTelemetryEndpoints();
app.MapTopologyEndpoints();
app.MapDebugEndpoints();
app.MapAttachmentEndpoints();
app.MapStreamEndpoints();
app.MapDeviceEndpoints();
app.MapCommandEndpoints();
app.MapInsightEndpoints();
app.MapMeshEndpoints();

app.MapHub<TelemetryHub>("/hub/telemetry");
app.MapHealthChecks("/health");

app.Run();
