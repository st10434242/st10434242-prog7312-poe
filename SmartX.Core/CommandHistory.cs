using SmartX.Core.Contracts;

namespace SmartX.Core;

// The undo stack, the last command pushed is always the first one undone
public sealed class CommandHistory
{
    private const int LogLimit = 100;

    private readonly Stack<DeviceCommand> _undo = new();

    // Separate from the stack so undone commands still show in the history list
    private readonly LinkedList<CommandRecordDto> _log = new();
    private readonly Lock _gate = new();

    public int UndoDepth
    {
        get
        {
            lock (_gate)
            {
                return _undo.Count;
            }
        }
    }

    public void Push(DeviceCommand command)
    {
        lock (_gate)
        {
            _undo.Push(command);
            _log.AddFirst(CommandRecordDto.From(command));

            if (_log.Count > LogLimit)
            {
                _log.RemoveLast();
            }
        }
    }

    public bool TryPeek(out DeviceCommand? command)
    {
        lock (_gate)
        {
            return _undo.TryPeek(out command);
        }
    }

    public bool TryUndo(out DeviceCommand? command)
    {
        lock (_gate)
        {
            if (!_undo.TryPop(out command))
            {
                return false;
            }

            // Flip the matching entry in the log to reverted
            for (LinkedListNode<CommandRecordDto>? node = _log.First; node is not null; node = node.Next)
            {
                if (node.Value.Id == command.Id)
                {
                    node.Value = node.Value with { Reverted = true };
                    break;
                }
            }

            return true;
        }
    }

    public IReadOnlyList<CommandRecordDto> Recent(int limit)
    {
        lock (_gate)
        {
            return [.. _log.Take(limit)];
        }
    }
}
