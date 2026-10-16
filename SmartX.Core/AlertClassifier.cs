using SmartX.Core.Contracts;
using static System.FormattableString;

namespace SmartX.Core;

// Looks at a packet as it comes in and decides if it is bad enough to jump the queue
public static class AlertClassifier
{
    public const double MoistureCriticalLow = 20;
    public const double MoistureWarningLow = 30;
    public const double MoistureWarningHigh = 80;
    public const double MoistureCriticalHigh = 90;
    public const int PowerWarning = 1000;
    public const int PowerCritical = 1200;

    public static AlertDto? Classify(TelemetryPacketDto dto)
    {
        switch (dto.Category)
        {
            case SensorCategory.Environmental when dto.TryReadFloat(out float moisture):
                return ClassifyMoisture(dto, moisture);

            case SensorCategory.PowerConsumption when dto.TryReadInt(out int watts):
                return ClassifyPower(dto, watts);

            default:
                // Valves report on or off so there is nothing to be out of range
                return null;
        }
    }

    private static AlertDto? ClassifyMoisture(TelemetryPacketDto dto, float moisture)
    {
        if (moisture < MoistureCriticalLow)
        {
            return Build(dto, AlertKind.MoistureDrop, AlertSeverity.Critical, moisture, Invariant($"Soil moisture at {moisture:0.0}%, critically dry"));
        }

        if (moisture < MoistureWarningLow)
        {
            return Build(dto, AlertKind.MoistureDrop, AlertSeverity.Warning, moisture, Invariant($"Soil moisture dropping, now {moisture:0.0}%"));
        }

        if (moisture >= MoistureCriticalHigh)
        {
            return Build(dto, AlertKind.MoistureFlood, AlertSeverity.Critical, moisture, Invariant($"Soil moisture at {moisture:0.0}%, node is flooding"));
        }

        if (moisture >= MoistureWarningHigh)
        {
            return Build(dto, AlertKind.MoistureFlood, AlertSeverity.Warning, moisture, Invariant($"Soil moisture high at {moisture:0.0}%"));
        }

        return null;
    }

    private static AlertDto? ClassifyPower(TelemetryPacketDto dto, int watts)
    {
        if (watts >= PowerCritical)
        {
            return Build(dto, AlertKind.PowerSpike, AlertSeverity.Critical, watts, Invariant($"Severe power spike, drawing {watts}W"));
        }

        if (watts >= PowerWarning)
        {
            return Build(dto, AlertKind.PowerSpike, AlertSeverity.Warning, watts, Invariant($"Power draw high at {watts}W"));
        }

        return null;
    }

    private static AlertDto Build(TelemetryPacketDto dto, AlertKind kind, AlertSeverity severity, double value, string message) => new()
    {
        NodeId = dto.NodeId,
        Kind = kind,
        Severity = severity,
        Value = Math.Round(value, 1),
        Message = message,
        RaisedAtUtc = dto.TimestampUtc
    };
}
