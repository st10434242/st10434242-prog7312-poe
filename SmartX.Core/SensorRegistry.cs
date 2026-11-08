using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace SmartX.Core;

// The gateway only accepts telemetry from nodes registered here
public sealed class SensorRegistry
{
    // Concurrent so no lock is needed, requests are handled in parallel
    private readonly ConcurrentDictionary<string, SensorRegistration> _byNodeId =
        new(StringComparer.OrdinalIgnoreCase);

    // Second index so a route can be traced by hardware address without scanning every node
    private readonly ConcurrentDictionary<MacAddress, string> _nodeIdByMac = new();

    public bool TryAdd(SensorRegistration registration)
    {
        // Claim the MAC first so two nodes racing for the same address can't both get in
        if (!_nodeIdByMac.TryAdd(registration.Mac, registration.NodeId))
        {
            return false;
        }

        if (_byNodeId.TryAdd(registration.NodeId, registration))
        {
            return true;
        }

        _nodeIdByMac.TryRemove(registration.Mac, out _);
        return false;
    }

    public bool TryGet(string nodeId, [NotNullWhen(true)] out SensorRegistration? registration) =>
        _byNodeId.TryGetValue(nodeId, out registration);

    public bool TryGetByMac(MacAddress mac, [NotNullWhen(true)] out SensorRegistration? registration)
    {
        registration = null;
        return _nodeIdByMac.TryGetValue(mac, out string? nodeId) && TryGet(nodeId, out registration);
    }

    // Accepts either a node id or a MAC, whichever the operator typed in
    public bool TryResolve(string nodeIdOrMac, [NotNullWhen(true)] out SensorRegistration? registration)
    {
        if (TryGet(nodeIdOrMac, out registration))
        {
            return true;
        }

        return MacAddress.TryParse(nodeIdOrMac, out MacAddress mac) && TryGetByMac(mac, out registration);
    }

    public bool Remove(string nodeId)
    {
        if (!_byNodeId.TryRemove(nodeId, out SensorRegistration? removed))
        {
            return false;
        }

        _nodeIdByMac.TryRemove(removed.Mac, out _);
        return true;
    }

    public IReadOnlyList<SensorRegistration> All() => [.. _byNodeId.Values];

    // Two nodes sharing a hardware address means one of them is misconfigured
    public bool IsMacInUse(MacAddress mac) => _nodeIdByMac.ContainsKey(mac);
}
