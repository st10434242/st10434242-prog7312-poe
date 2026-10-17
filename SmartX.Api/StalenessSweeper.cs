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
    DeviceTable devices,
    TimelineLog timeline,
    FaultSet faultSet,
    AlertQueue alerts,
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

            devices.SetLiveness(sensor.NodeId, state);
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

            // Only a full disconnect counts, and the set makes sure it is only raised once per dropout
            if (state == LivenessState.Stale && faultSet.MarkDisconnected(sensor.NodeId))
            {
                AlertDto alert = new()
                {
                    NodeId = sensor.NodeId,
                    Kind = AlertKind.Disconnected,
                    Severity = AlertSeverity.Critical,
                    Message = $"{sensor.NodeId} has stopped reporting",
                    RaisedAtUtc = now
                };

                // No packet to store so it skips the queue and is just recorded and pushed
                alerts.MarkProcessed(alert);
                await hub.Clients.All.SendAsync("AlertRaised", alert, cancellationToken);

                TimelineEventDto timelineEvent = new()
                {
                    TimestampUtc = now,
                    Kind = TimelineEventKind.Disconnected,
                    NodeId = sensor.NodeId,
                    Message = $"{sensor.NodeId} stopped reporting"
                };

                timeline.LogEvent(timelineEvent);
                await hub.Clients.All.SendAsync("EventLogged", timelineEvent, cancellationToken);
            }
        }
    }
}
