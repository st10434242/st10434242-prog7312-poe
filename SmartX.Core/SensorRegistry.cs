using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace SmartX.Core;

// The gateway only accepts telemetry from nodes registered here
public sealed class SensorRegistry
{
    // Concurrent so no lock is needed, requests are handled in parallel
    private readonly ConcurrentDictionary<string, SensorRegistration> _byNodeId =
        new(StringComparer.OrdinalIgnoreCase);

    public bool TryAdd(SensorRegistration registration) =>
        _byNodeId.TryAdd(registration.NodeId, registration);

    public bool TryGet(string nodeId, [NotNullWhen(true)] out SensorRegistration? registration) =>
        _byNodeId.TryGetValue(nodeId, out registration);

    public bool Remove(string nodeId) => _byNodeId.TryRemove(nodeId, out _);

    public IReadOnlyList<SensorRegistration> All() => [.. _byNodeId.Values];

    // Two nodes sharing a hardware address means one of them is misconfigured
    public bool IsMacInUse(MacAddress mac) => _byNodeId.Values.Any(r => r.Mac == mac);
}
