using SmartX.Core;
using SmartX.Core.Contracts;

namespace SmartX.Simulator;

// Metres from the corner of the facility floor
public readonly record struct FloorPosition(double X, double Y)
{
    public double DistanceTo(FloorPosition other) => Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2));
}

// A device's radio, either a sensor or the gateway
public readonly record struct RadioNode(string NodeId, FloorPosition Position);

// Places every device on the floor and works out who can hear who
public static class MeshLayout
{
    // Each sub-zone gets its own patch of floor, zones run left to right and sub-zones top to bottom
    private const double CellWidth = 35;
    private const double CellHeight = 30;
    private const double CellMargin = 4;

    // Roughly what an ESP32 hears one metre away at its default transmit power
    private const double ReferenceRssiDbm = -40;

    // Free space would be 2, racks, walls and water tanks in a grow room push it a lot higher
    private const double PathLossExponent = 3.2;

    // How much a wall or rack between two nodes can add or take away
    private const double MaxShadowingDb = 4;

    // The gateway sits just off the left edge of zone 1, so the far zones have to hop to reach it
    public static FloorPosition GatewayPosition(SimulatorOptions options) =>
        new(-5, Math.Round(options.SubZonesPerZone * CellHeight / 2, 1));

    public static FloorPosition PositionFor(string nodeId, int zone, int subZone)
    {
        // Seeded from the id so the floor plan is the same every time the simulator starts
        Random random = new(StableSeed(nodeId));

        double left = (zone - 1) * CellWidth;
        double top = (subZone - 1) * CellHeight;

        double x = left + CellMargin + (random.NextDouble() * (CellWidth - (2 * CellMargin)));
        double y = top + CellMargin + (random.NextDouble() * (CellHeight - (2 * CellMargin)));

        return new FloorPosition(Math.Round(x, 1), Math.Round(y, 1));
    }

    // Log distance path loss, signal falls off with the log of the distance
    public static int RssiBetween(RadioNode a, RadioNode b)
    {
        double distance = Math.Max(a.Position.DistanceTo(b.Position), 1);
        double rssi = ReferenceRssiDbm - (10 * PathLossExponent * Math.Log10(distance));

        return (int)Math.Round(rssi + Shadowing(a.NodeId, b.NodeId));
    }

    // Only the strongest few make the table, like the limited peer list on a real chip
    public static NeighbourReportDto ReportFor(RadioNode node, IReadOnlyList<RadioNode> awake, int maxNeighbours)
    {
        List<NeighbourDto> neighbours = [.. awake
            .Where(other => other.NodeId != node.NodeId)
            .Select(other => new NeighbourDto { NodeId = other.NodeId, RssiDbm = RssiBetween(node, other) })
            .Where(n => MeshRadio.IsUsable(n.RssiDbm))
            .OrderByDescending(n => n.RssiDbm)
            .Take(maxNeighbours)];

        return new NeighbourReportDto
        {
            NodeId = node.NodeId,
            X = node.Position.X,
            Y = node.Position.Y,
            Neighbours = neighbours
        };
    }

    private static double Shadowing(string a, string b)
    {
        // Ordered so both ends of the link see the same wall
        string pair = string.CompareOrdinal(a, b) < 0 ? $"{a}|{b}" : $"{b}|{a}";
        Random random = new(StableSeed(pair));

        return (random.NextDouble() * 2 * MaxShadowingDb) - MaxShadowingDb;
    }

    // string.GetHashCode changes every run, this one doesn't
    private static int StableSeed(string text)
    {
        uint hash = 2166136261;

        foreach (char c in text)
        {
            hash ^= c;
            hash *= 16777619;
        }

        return (int)hash;
    }
}
