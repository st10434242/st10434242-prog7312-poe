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

builder.Services.AddHostedService<StalenessSweeper>();

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

app.MapHub<TelemetryHub>("/hub/telemetry");
app.MapHealthChecks("/health");

app.Run();
