using SmartX.Core.Contracts;

namespace SmartX.Core;

// Commands waiting for a device to check in, each device gets its own FIFO so they apply in the order sent
public sealed class CommandOutbox
{
    private readonly Dictionary<string, Queue<PendingCommandDto>> _byNode = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    public void Enqueue(PendingCommandDto command)
    {
        lock (_gate)
        {
            if (!_byNode.TryGetValue(command.NodeId, out Queue<PendingCommandDto>? queue))
            {
                queue = new Queue<PendingCommandDto>();
                _byNode[command.NodeId] = queue;
            }

            queue.Enqueue(command);
        }
    }

    // A device takes everything waiting for it in one go
    public IReadOnlyList<PendingCommandDto> Collect(string nodeId)
    {
        lock (_gate)
        {
            if (!_byNode.TryGetValue(nodeId, out Queue<PendingCommandDto>? queue))
            {
                return [];
            }

            List<PendingCommandDto> collected = [];

            while (queue.TryDequeue(out PendingCommandDto? command))
            {
                collected.Add(command);
            }

            return collected;
        }
    }

    public int PendingFor(string nodeId)
    {
        lock (_gate)
        {
            return _byNode.TryGetValue(nodeId, out Queue<PendingCommandDto>? queue) ? queue.Count : 0;
        }
    }

    public void Remove(string nodeId)
    {
        lock (_gate)
        {
            _byNode.Remove(nodeId);
        }
    }
}
