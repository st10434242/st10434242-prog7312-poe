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

        while (!stoppingToken.IsCancellationRequested)
        {
            await PublishAsync(client, stoppingToken);

            try
            {
                await Task.Delay(_options.PublishIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
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
}
