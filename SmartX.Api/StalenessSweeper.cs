using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Hubs;
using SmartX.Core;
using SmartX.Core.Contracts;

namespace SmartX.Api;

// A dead sensor sends nothing, and nothing is exactly what fails to trigger a request handler.
// This timer is the only thing that can turn silence into an event.
public sealed class StalenessSweeper(
    SensorRegistry registry,
    TelemetryStore store,
    NodeStateTracker tracker,
    IHubContext<TelemetryHub> hub,
    ILogger<StalenessSweeper> logger) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(SweepInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SweepAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (SensorRegistration sensor in registry.All())
        {
            DateTimeOffset? lastSeen = store.LastSeenUtc(sensor);

            // Judged against this node's own cadence, not one timeout for the whole fleet
            LivenessState state = NodeStatus.LivenessFor(lastSeen, sensor.ExpectedInterval, now);

            if (!tracker.Update(sensor.NodeId, state))
            {
                continue;
            }

            logger.LogInformation("{NodeId} is now {State}", sensor.NodeId, state);

            await hub.Clients.All.SendAsync(
                "LivenessChanged",
                new LivenessEventDto
                {
                    NodeId = sensor.NodeId,
                    Liveness = state,
                    LastSeenUtc = lastSeen
                },
                cancellationToken);
        }
    }
}
