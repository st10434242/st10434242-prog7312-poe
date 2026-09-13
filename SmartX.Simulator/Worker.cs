using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SmartX.Core;
using SmartX.Core.Contracts;

namespace SmartX.Simulator;

// Stands in for an ESP32 publishing to the gateway. Just one node right now i can make a whole fleet later 
public class Worker(
    ILogger<Worker> logger,
    IHttpClientFactory httpClientFactory,
    IOptions<SimulatorOptions> options) : BackgroundService
{
    public const string HttpClientName = "gateway";

    private readonly SimulatorOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Publishing as {NodeId} to {Url} every {Interval}ms",
            _options.NodeId,
            _options.ApiBaseUrl,
            _options.PublishIntervalMs);

        HttpClient client = httpClientFactory.CreateClient(HttpClientName);

        // The gateway rejects unregistered nodes, so keep trying until it accepts us
        while (!await TryRegisterAsync(client, stoppingToken))
        {
            if (!await DelayAsync(stoppingToken))
            {
                return;
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await PublishAsync(client, stoppingToken);

            if (!await DelayAsync(stoppingToken))
            {
                return;
            }
        }
    }

    private async Task<bool> TryRegisterAsync(HttpClient client, CancellationToken cancellationToken)
    {
        SensorRegistrationDto registration = new()
        {
            NodeId = _options.NodeId,
            Mac = _options.Mac,
            Location = _options.Location,
            Category = SensorCategory.Environmental,
            ExpectedIntervalMs = _options.PublishIntervalMs
        };

        try
        {
            HttpResponseMessage response =
                await client.PostAsJsonAsync("/api/sensors", registration, cancellationToken);

            // Conflict just means the gateway already knows us from a previous run
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict)
            {
                logger.LogInformation("Registered {NodeId}", _options.NodeId);
                return true;
            }

            logger.LogWarning("Registration rejected ({Status})", (int)response.StatusCode);
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Gateway unreachable: {Message}", ex.Message);
            return false;
        }
    }

    private async Task PublishAsync(HttpClient client, CancellationToken cancellationToken)
    {
        float reading = 40f + (Random.Shared.NextSingle() * 5f);

        TelemetryPacketDto packet = new()
        {
            NodeId = _options.NodeId,
            TimestampUtc = DateTimeOffset.UtcNow,
            Category = SensorCategory.Environmental,
            ValueKind = TelemetryValueKind.Float,
            Value = JsonSerializer.SerializeToElement(reading)
        };

        try
        {
            HttpResponseMessage response =
                await client.PostAsJsonAsync("/api/telemetry", packet, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Published {Reading:F2} ({Status})", reading, (int)response.StatusCode);
            }
            else
            {
                logger.LogWarning("Gateway rejected the packet ({Status})", (int)response.StatusCode);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down, not a failure
        }
        catch (HttpRequestException ex)
        {
            // The loop is the retry, so a gateway that is not up yet just means waiting a tick
            logger.LogWarning("Gateway unreachable: {Message}", ex.Message);
        }
    }

    // Returns false when the host is shutting down
    private async Task<bool> DelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_options.PublishIntervalMs, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
