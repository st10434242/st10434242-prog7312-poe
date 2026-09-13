namespace SmartX.Core;

public sealed record ZoneSummary
{
    public required string Name { get; init; }

    public required int SensorCount { get; init; }

    public required int TotalWatts { get; init; }

    public required int ActuatorCount { get; init; }

    public required bool AllActuatorsOpen { get; init; }

    public required bool AnyActuatorOpen { get; init; }

    public required IReadOnlyList<string> NodeIds { get; init; }

    public required IReadOnlyList<ZoneSummary> Children { get; init; }
}

// The two recursive walks: down the tree to validate placement, back up it to aggregate
public static class DeploymentTree
{
    public static IReadOnlyList<string> Validate(DeploymentNode root)
    {
        List<string> errors = [];
        ValidateLevel(root, 0, new HashSet<string>(StringComparer.OrdinalIgnoreCase), errors);
        return errors;
    }

    private static void ValidateLevel(
        DeploymentNode node,
        int depth,
        HashSet<string> seenNodeIds,
        List<string> errors)
    {
        // Base case, stops the descent before it can run away on a malformed tree
        if (depth > DeploymentPath.MaxDepth)
        {
            errors.Add($"'{node.Name}' sits deeper than the {DeploymentPath.MaxDepth} level limit.");
            return;
        }

        foreach (SensorRegistration sensor in node.Sensors)
        {
            if (!seenNodeIds.Add(sensor.NodeId))
            {
                errors.Add($"Node '{sensor.NodeId}' appears in more than one place in the tree.");
            }
        }

        // A level holding neither sensors nor sub-levels is an orphan
        if (depth > 0 && node.Children.Count == 0 && node.Sensors.Count == 0)
        {
            errors.Add($"'{node.Name}' has no sensors and no sub-levels.");
        }

        foreach (DeploymentNode child in node.Children)
        {
            ValidateLevel(child, depth + 1, seenNodeIds, errors);
        }
    }

    public static ZoneSummary Summarise(DeploymentNode node, TelemetryStore store)
    {
        // Recurse first, a level's totals are its own sensors plus everything beneath it
        List<ZoneSummary> children = [.. node.Children.Select(child => Summarise(child, store))];

        MeterReading<int> watts = new(0);
        ActuatorReading allOpen = new(true);
        ActuatorReading anyOpen = new(false);

        int sensorCount = node.Sensors.Count;
        int actuatorCount = 0;
        List<string> nodeIds = [];

        foreach (SensorRegistration sensor in node.Sensors)
        {
            nodeIds.Add(sensor.NodeId);

            if (sensor.Category == SensorCategory.PowerConsumption &&
                TryLatest(store.Ints, sensor.NodeId, out int reading))
            {
                watts += new MeterReading<int>(reading);
            }
            else if (sensor.Category == SensorCategory.Actuator &&
                     TryLatest(store.Bools, sensor.NodeId, out bool isOpen))
            {
                actuatorCount++;
                allOpen &= new ActuatorReading(isOpen);
                anyOpen |= new ActuatorReading(isOpen);
            }
        }

        foreach (ZoneSummary child in children)
        {
            sensorCount += child.SensorCount;
            actuatorCount += child.ActuatorCount;
            watts += new MeterReading<int>(child.TotalWatts);

            if (child.ActuatorCount > 0)
            {
                allOpen &= new ActuatorReading(child.AllActuatorsOpen);
                anyOpen |= new ActuatorReading(child.AnyActuatorOpen);
            }
        }

        return new ZoneSummary
        {
            Name = node.Name,
            SensorCount = sensorCount,
            TotalWatts = watts.Value,
            ActuatorCount = actuatorCount,
            // With no actuators underneath, "all open" would otherwise report true from the identity
            AllActuatorsOpen = actuatorCount > 0 && allOpen.IsOpen,
            AnyActuatorOpen = anyOpen.IsOpen,
            NodeIds = nodeIds,
            Children = children
        };
    }

    private static bool TryLatest<T>(NodeTelemetryStore<T> store, string nodeId, out T value)
        where T : struct
    {
        IReadOnlyList<TelemetryPacket<T>> recent = store.GetRecent(nodeId);

        if (recent.Count == 0)
        {
            value = default;
            return false;
        }

        value = recent[^1].Value;
        return true;
    }
}
