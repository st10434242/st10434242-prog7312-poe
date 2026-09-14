using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using SmartX.Core.Contracts;

namespace SmartX.Simulator;

// Stands in for the ESP32 fleet publishing to the gateway
public class Worker(
    ILogger<Worker> logger,
    IHttpClientFactory httpClientFactory,
    IOptions<SimulatorOptions> options) : BackgroundService
{
    public const string HttpClientName = "gateway";

    private readonly SimulatorOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        List<SimulatedSensor> fleet = FleetBuilder.Build(_options);

        logger.LogInformation(
            "Simulating {Count} sensors across {Facility}, publishing to {Url} every {Interval}ms",
            fleet.Count,
            _options.Facility,
            _options.ApiBaseUrl,
            _options.PublishIntervalMs);

        HttpClient client = httpClientFactory.CreateClient(HttpClientName);

        // The gateway rejects unregistered nodes, so keep trying until the whole fleet is known
        while (!await TryRegisterFleetAsync(client, fleet, stoppingToken))
        {
            if (!await DelayAsync(stoppingToken))
            {
                return;
            }
        }

        long tick = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            await PublishTickAsync(client, fleet, tick, stoppingToken);
            tick++;

            if (!await DelayAsync(stoppingToken))
            {
                return;
            }
        }
    }

    private async Task<bool> TryRegisterFleetAsync(
        HttpClient client,
        List<SimulatedSensor> fleet,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (SimulatedSensor sensor in fleet)
            {
                SensorRegistrationDto registration = new()
                {
                    NodeId = sensor.NodeId,
                    Mac = sensor.Mac,
                    Location = sensor.Location,
                    Category = sensor.Category,
                    ExpectedIntervalMs = _options.PublishIntervalMs * sensor.PublishEveryNTicks
                };

                HttpResponseMessage response =
                    await client.PostAsJsonAsync("/api/sensors", registration, cancellationToken);

                // Conflict just means the gateway already knows this node from a previous run
                if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Conflict)
                {
                    logger.LogWarning(
                        "Registration rejected for {NodeId} ({Status})",
                        sensor.NodeId,
                        (int)response.StatusCode);

                    return false;
                }
            }

            logger.LogInformation("Registered {Count} sensors", fleet.Count);
            return true;
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

    private async Task PublishTickAsync(
        HttpClient client,
        List<SimulatedSensor> fleet,
        long tick,
        CancellationToken cancellationToken)
    {
        List<TelemetryPacketDto> batch = [];
        int spikes = 0;

        foreach (SimulatedSensor sensor in fleet)
        {
            sensor.UpdateFaults(_options.SilenceChance, _options.RecoverChance);

            if (!sensor.ShouldPublish(tick))
            {
                continue;
            }

            batch.Add(sensor.NextReading(_options.SpikeChance, out bool spiked));

            if (spiked)
            {
                spikes++;
            }
        }

        if (batch.Count == 0)
        {
            return;
        }

        try
        {
            // One request per tick rather than one per sensor, which is what the batch endpoint is for
            HttpResponseMessage response =
                await client.PostAsJsonAsync("/api/telemetry/batch", batch, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Gateway rejected the batch ({Status})", (int)response.StatusCode);
                return;
            }

            BatchResultDto? result =
                await response.Content.ReadFromJsonAsync<BatchResultDto>(cancellationToken);

            // A batch accepted with nothing stored means the gateway has forgotten these nodes, which happens whenever it restarts, since the registry is held in memory
            if (result is { Accepted: 0, Rejected: > 0 })
            {
                logger.LogWarning(
                    "Gateway stored none of {Count} readings, registering the fleet again",
                    batch.Count);

                await TryRegisterFleetAsync(client, fleet, cancellationToken);
                return;
            }

            logger.LogInformation(
                "Published {Count} readings ({Silent} silent, {Spikes} spiking)",
                result?.Accepted ?? batch.Count,
                fleet.Count(s => s.IsSilent),
                spikes);
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
