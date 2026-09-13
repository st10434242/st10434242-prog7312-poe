using SmartX.Api.Endpoints;
using SmartX.Core;

var builder = WebApplication.CreateBuilder(args);

// Singleton because the store is the gateway's memory for the lifetime of the process
builder.Services.AddSingleton<TelemetryStore>();

builder.Services.AddOpenApi();

// Makes malformed requests come back as ProblemDetails instead of a raw exception dump
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapTelemetryEndpoints();

app.Run();
