using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Hubs;
using SmartX.Core;
using SmartX.Core.Contracts;

namespace SmartX.Api;

// Recalculates the suggestions every couple of seconds and only pushes them when something actually changed
public sealed class InsightPublisher(
    InsightEngine engine,
    IHubContext<TelemetryHub> hub,
    ILogger<InsightPublisher> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);
    private const int Limit = 8;

    private string _lastSignature = string.Empty;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    IReadOnlyList<InsightDto> insights = engine.Build(Limit);

                    // Ids and scores are enough to tell if the list is different
                    string signature = string.Join('|', insights.Select(i => $"{i.Id}:{i.Score}"));

                    if (signature == _lastSignature)
                    {
                        continue;
                    }

                    _lastSignature = signature;
                    await hub.Clients.All.SendAsync("InsightsUpdated", insights, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Failed to build insights");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down
        }
    }
}
