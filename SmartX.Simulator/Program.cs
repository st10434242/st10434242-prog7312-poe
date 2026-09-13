using SmartX.Simulator;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<SimulatorOptions>(
    builder.Configuration.GetSection(SimulatorOptions.SectionName));

SimulatorOptions options = builder.Configuration
    .GetSection(SimulatorOptions.SectionName)
    .Get<SimulatorOptions>() ?? new SimulatorOptions();

// The factory pools handlers, so the worker avoids both socket exhaustion and stale DNS
builder.Services.AddHttpClient(Worker.HttpClientName, client =>
{
    client.BaseAddress = new Uri(options.ApiBaseUrl);
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
