using SmartX.Api.Endpoints;
using SmartX.Core;

var builder = WebApplication.CreateBuilder(args);

// Singleton because these are the gateway's memory for the lifetime of the process
builder.Services.AddSingleton<TelemetryStore>();
builder.Services.AddSingleton<SensorRegistry>();

builder.Services.AddOpenApi();

// Makes malformed requests come back as ProblemDetails instead of a raw exception dump
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapSensorEndpoints();
app.MapTelemetryEndpoints();

app.Run();
