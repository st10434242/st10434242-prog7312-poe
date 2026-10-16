using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Hubs;
using SmartX.Core;
using SmartX.Core.Contracts;

namespace SmartX.Api;

// Drains the telemetry queue in arrival order, this is where packets actually get stored and broadcast
public sealed class QueueProcessor(
    TelemetryQueue queue,
    TelemetryStore store,
    NodeStateTracker tracker,
    IHubContext<TelemetryHub> hub,
    ILogger<QueueProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                while (queue.TryDequeue(out TelemetryPacketDto? packet))
                {
                    await ProcessAsync(packet!, stoppingToken);
                }

                // Nothing left so sleep until an endpoint enqueues something
                await queue.WaitForPacketsAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down
        }
    }

    private async Task ProcessAsync(TelemetryPacketDto dto, CancellationToken cancellationToken)
    {
        try
        {
            ReadingEventDto? reading = StoreReading(dto);

            if (reading is null)
            {
                logger.LogWarning("Dropped packet from {NodeId} with value kind {Kind}", dto.NodeId, dto.ValueKind);
                return;
            }

            // A packet just arrived so the node is live
            tracker.Update(reading.NodeId, LivenessState.Live);

            await hub.Clients.All.SendAsync("ReadingReceived", reading, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One bad packet should not kill the processor for everyone else
            logger.LogError(ex, "Failed to process packet from {NodeId}", dto.NodeId);
        }
        finally
        {
            queue.MarkProcessed();
        }
    }

    // The only place an untyped payload becomes a typed packet
    private ReadingEventDto? StoreReading(TelemetryPacketDto dto)
    {
        switch (dto.ValueKind)
        {
            case TelemetryValueKind.Float:
                dto.TryReadFloat(out float floatValue);
                store.Floats.Add(ToPacket(dto, floatValue));
                return ToEvent(dto, floatValue, floatValue);

            case TelemetryValueKind.Int:
                dto.TryReadInt(out int intValue);
                store.Ints.Add(ToPacket(dto, intValue));
                return ToEvent(dto, intValue, intValue);

            case TelemetryValueKind.Bool:
                dto.TryReadBool(out bool boolValue);
                store.Bools.Add(ToPacket(dto, boolValue));
                return ToEvent(dto, boolValue, 0);

            default:
                return null;
        }
    }

    private static ReadingEventDto ToEvent(TelemetryPacketDto dto, object value, double numeric) => new()
    {
        NodeId = dto.NodeId,
        TimestampUtc = dto.TimestampUtc,
        Value = value,
        Band = NodeStatus.BandFor(dto.Category, numeric)
    };

    private static TelemetryPacket<T> ToPacket<T>(TelemetryPacketDto dto, T value) where T : struct =>
        new(dto.NodeId, dto.TimestampUtc, dto.Category, value);
}
