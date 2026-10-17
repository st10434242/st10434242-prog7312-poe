using SmartX.Core.Contracts;

namespace SmartX.Core;

// Learns "after X the operator usually does Y" by counting which action follows which, basically a weighted graph
public sealed class PatternLearner
{
    // Only link things that happened close together, an alert from an hour ago did not cause this search
    private static readonly TimeSpan LinkWindow = TimeSpan.FromMinutes(2);
    private const int MaxLinkedAlerts = 5;
    private const int MinSupport = 2;

    // from key -> (to key -> times seen)
    private readonly Dictionary<string, Dictionary<string, int>> _transitions = [];

    // One example per key so a key can be turned back into a command or device
    private readonly Dictionary<string, OperatorActivity> _examples = [];
    private readonly Queue<OperatorActivity> _recentAlerts = new();
    private readonly Lock _gate = new();

    private OperatorActivity? _lastOperatorAction;

    public void Record(OperatorActivity activity)
    {
        lock (_gate)
        {
            _examples[activity.Key] = activity;
            TrimAlerts(activity.TimestampUtc);

            // Alerts only ever start a pattern, the network isn't the one we are learning from
            if (!activity.IsOperator)
            {
                _recentAlerts.Enqueue(activity);
                return;
            }

            // Newest alerts first, and each alert only counted once even if it fired a few times
            IEnumerable<string> alertSources = _recentAlerts
                .Reverse()
                .Select(a => a.Key)
                .Distinct()
                .Take(MaxLinkedAlerts);

            foreach (string source in alertSources)
            {
                Link(source, activity.Key);
            }

            if (_lastOperatorAction is not null &&
                _lastOperatorAction.Key != activity.Key &&
                activity.TimestampUtc - _lastOperatorAction.TimestampUtc <= LinkWindow)
            {
                Link(_lastOperatorAction.Key, activity.Key);
            }

            _lastOperatorAction = activity;
        }
    }

    // What usually comes after this key, strongest first, ignoring anything only seen once
    public IReadOnlyList<(OperatorActivity Next, int Count, double Confidence)> Followers(string key)
    {
        lock (_gate)
        {
            if (!_transitions.TryGetValue(key, out Dictionary<string, int>? next))
            {
                return [];
            }

            int total = next.Values.Sum();

            return [.. next
                .Where(n => n.Value >= MinSupport)
                .OrderByDescending(n => n.Value)
                .Select(n => (_examples[n.Key], n.Value, (double)n.Value / total))];
        }
    }

    public OperatorActivity? LastOperatorAction(TimeSpan within)
    {
        lock (_gate)
        {
            if (_lastOperatorAction is null || DateTimeOffset.UtcNow - _lastOperatorAction.TimestampUtc > within)
            {
                return null;
            }

            return _lastOperatorAction;
        }
    }

    // For the patterns endpoint, shows what has been learned so far
    public IReadOnlyList<PatternDto> Patterns(int limit)
    {
        lock (_gate)
        {
            List<PatternDto> patterns = [];

            foreach ((string from, Dictionary<string, int> next) in _transitions)
            {
                int total = next.Values.Sum();

                foreach ((string to, int count) in next)
                {
                    patterns.Add(new PatternDto(from, to, count, Math.Round((double)count / total, 2)));
                }
            }

            return [.. patterns.OrderByDescending(p => p.Count).ThenByDescending(p => p.Confidence).Take(limit)];
        }
    }

    private void Link(string from, string to)
    {
        if (!_transitions.TryGetValue(from, out Dictionary<string, int>? next))
        {
            next = [];
            _transitions[from] = next;
        }

        next[to] = next.GetValueOrDefault(to) + 1;
    }

    private void TrimAlerts(DateTimeOffset now)
    {
        // Oldest are at the front so stop at the first one still inside the window
        while (_recentAlerts.TryPeek(out OperatorActivity? oldest) && now - oldest.TimestampUtc > LinkWindow)
        {
            _recentAlerts.Dequeue();
        }
    }
}
