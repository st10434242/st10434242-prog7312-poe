using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using SmartX.Core;
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

        // Grouped by sub-zone so a pump or relay only affects the sensors next to it
        Dictionary<string, List<SimulatedSensor>> bySubZone = fleet
            .GroupBy(s => s.Location)
            .ToDictionary(g => g.Key, g => g.ToList());

        logger.LogInformation(
            "Simulating {Count} devices across {Facility}, publishing to {Url} every {Interval}ms",
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
            await CollectCommandsAsync(client, fleet, stoppingToken);
            await PublishTickAsync(client, fleet, bySubZone, tick, stoppingToken);

            if (tick % _options.NeighbourReportEveryNTicks == 0)
            {
                await ReportNeighboursAsync(client, fleet, stoppingToken);
            }

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

            logger.LogInformation("Registered {Count} devices", fleet.Count);
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

    // Each actuator checks in for its own commands like a real device would, a silent one can't hear the gateway so it doesn't
    private async Task CollectCommandsAsync(
        HttpClient client,
        List<SimulatedSensor> fleet,
        CancellationToken cancellationToken)
    {
        IEnumerable<Task> checkIns = fleet
            .Where(s => s.Actuator is not null && !s.IsSilent)
            .Select(s => CollectForAsync(client, s, cancellationToken));

        await Task.WhenAll(checkIns);
    }

    private async Task CollectForAsync(HttpClient client, SimulatedSensor actuator, CancellationToken cancellationToken)
    {
        try
        {
            List<PendingCommandDto>? commands = await client.GetFromJsonAsync<List<PendingCommandDto>>(
                $"/api/commands/pending/{Uri.EscapeDataString(actuator.NodeId)}",
                cancellationToken);

            // Applied in the order they were sent, so the last one wins
            foreach (PendingCommandDto command in commands ?? [])
            {
                if (actuator.Apply(command))
                {
                    logger.LogInformation("{NodeId} switched {State} by command", actuator.NodeId, command.DesiredState ? "on" : "off");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down
        }
        catch (HttpRequestException)
        {
            // Gateway down or forgot the node, the publish loop already handles both
        }
    }

    private async Task PublishTickAsync(
        HttpClient client,
        List<SimulatedSensor> fleet,
        Dictionary<string, List<SimulatedSensor>> bySubZone,
        long tick,
        CancellationToken cancellationToken)
    {
        Dictionary<string, SubZoneConditions> conditions = bySubZone.ToDictionary(g => g.Key, g => ConditionsFor(g.Value));

        List<TelemetryPacketDto> batch = [];
        int spikes = 0;
        int flushed = 0;

        foreach (SimulatedSensor sensor in fleet)
        {
            sensor.UpdateFaults(_options.SilenceChance, _options.RecoverChance);

            if (!sensor.IsDue(tick))
            {
                continue;
            }

            TelemetryPacketDto reading = sensor.NextReading(_options, conditions[sensor.Location], out bool spiked);

            if (sensor.IsSilent)
            {
                sensor.Hold(reading);
                continue;
            }

            // Back online, so anything held while it was quiet goes out first
            if (sensor.BacklogCount > 0)
            {
                List<TelemetryPacketDto> held = sensor.FlushBacklog();
                batch.AddRange(held);
                flushed += held.Count;
            }

            batch.Add(reading);

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
                "Published {Count} readings ({Silent} silent, {Spikes} abnormal, {Flushed} from backlog)",
                result?.Accepted ?? batch.Count,
                fleet.Count(s => s.IsSilent),
                spikes,
                flushed);
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

    // Every awake node beacons who it can hear, a silent node neither reports nor shows up in anyone else's table
    private async Task ReportNeighboursAsync(
        HttpClient client,
        List<SimulatedSensor> fleet,
        CancellationToken cancellationToken)
    {
        List<RadioNode> awake = [.. fleet
            .Where(s => !s.IsSilent)
            .Select(s => new RadioNode(s.NodeId, s.Position))];

        // The gateway's radio beacons like everyone else so the graph knows where it sits
        awake.Add(new RadioNode(MeshRadio.GatewayId, MeshLayout.GatewayPosition(_options)));

        IEnumerable<Task<bool>> posts = awake
            .Select(node => MeshLayout.ReportFor(node, awake, _options.MaxNeighbours))
            .Select(report => PostReportAsync(client, report, cancellationToken));

        bool[] results = await Task.WhenAll(posts);
        int accepted = results.Count(r => r);

        if (accepted < results.Length)
        {
            logger.LogWarning("Gateway took {Accepted} of {Count} neighbour reports", accepted, results.Length);
        }
    }

    private static async Task<bool> PostReportAsync(
        HttpClient client,
        NeighbourReportDto report,
        CancellationToken cancellationToken)
    {
        try
        {
            HttpResponseMessage response =
                await client.PostAsJsonAsync("/api/mesh/neighbours", report, cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (HttpRequestException)
        {
            // Same as readings, the next report is the retry
            return false;
        }
    }

    // Water only flows if the pump is running and at least one valve is open, the relay feeds the meters
    private static SubZoneConditions ConditionsFor(List<SimulatedSensor> subZone)
    {
        List<SimulatedSensor> valves = [.. subZone.Where(s => s.Actuator == ActuatorKind.Valve)];
        List<SimulatedSensor> pumps = [.. subZone.Where(s => s.Actuator == ActuatorKind.Pump)];
        List<SimulatedSensor> relays = [.. subZone.Where(s => s.Actuator == ActuatorKind.Relay)];

        bool pumpRunning = pumps.Count == 0 || pumps.Any(p => p.IsOn);
        bool valveOpen = valves.Count == 0 || valves.Any(v => v.IsOn);
        bool powered = relays.Count == 0 || relays.Any(r => r.IsOn);

        return new SubZoneConditions(pumpRunning && valveOpen, powered);
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
