namespace SmartX.Core;

// Radio numbers for a 2.4 GHz ESP32 style mesh, shared by the gateway and the simulator
public static class MeshRadio
{
    // The gateway is a vertex in the graph like any other node, it just never goes down
    public const string GatewayId = "GATEWAY";

    // Below this the radio can't decode a frame at all, so there is no link
    public const int SensitivityDbm = -90;

    // Anything at or above this is treated as a perfect link
    public const int StrongDbm = -50;

    // What every hop costs even on a perfect link (receive, wake up, forward)
    public const double BaseHopCost = 10;

    public static bool IsUsable(int rssiDbm) => rssiDbm >= SensitivityDbm;

    // Weak links drop more frames, so a packet needs more retries and more transmit energy to get across.
    // Quality 1 means one transmission, quality 0.25 means roughly four.
    public static double CostFor(int rssiDbm)
    {
        double quality = (double)(rssiDbm - SensitivityDbm) / (StrongDbm - SensitivityDbm);
        quality = Math.Clamp(quality, 0.05, 1);

        double expectedTransmissions = 1 / quality;

        return Math.Round(BaseHopCost * expectedTransmissions, 2);
    }
}
