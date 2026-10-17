using SmartX.Core.Contracts;

namespace SmartX.Core;

// Works out what the operator should look at or do next, from what is broken right now and what they did last time
public sealed class InsightEngine(DeviceTable devices, FaultSet faults, PatternLearner learner, CommandOutbox outbox)
{
    private static readonly TimeSpan DismissFor = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RecentActionWindow = TimeSpan.FromMinutes(5);

    private readonly Dictionary<string, DateTimeOffset> _dismissedUntil = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<InsightDto> Build(int limit)
    {
        Dictionary<string, InsightDto> byId = [];
        FaultsDto snapshot = faults.Snapshot();

        // Rule pass, anything in the fault sets gets matched to an actuator next to it that could fix it
        foreach (ActiveErrorDto error in snapshot.ActiveErrors)
        {
            foreach (InsightDto insight in RulesFor(error))
            {
                Add(byId, insight);
            }
        }

        foreach (string nodeId in snapshot.Disconnected)
        {
            Add(byId, DisconnectedInsight(nodeId));
        }

        // Learned pass, things that are happening now that the operator has reacted to before
        List<OperatorActivity> triggers =
        [
            .. snapshot.ActiveErrors.Select(e => OperatorActivity.ForAlert(e.Kind, e.NodeId)),
            .. snapshot.Disconnected.Select(n => OperatorActivity.ForAlert(AlertKind.Disconnected, n))
        ];

        OperatorActivity? lastAction = learner.LastOperatorAction(RecentActionWindow);

        if (lastAction is not null)
        {
            triggers.Add(lastAction);
        }

        // An error can be in the set as a warning and a critical, no point asking twice
        foreach (OperatorActivity trigger in triggers.DistinctBy(t => t.Key))
        {
            foreach ((OperatorActivity next, int count, double confidence) in learner.Followers(trigger.Key))
            {
                InsightDto? insight = LearnedInsight(trigger, next, count, confidence);

                if (insight is not null)
                {
                    Add(byId, insight);
                }
            }
        }

        return Rank(byId.Values, limit);
    }

    public void Dismiss(string id)
    {
        lock (_gate)
        {
            _dismissedUntil[id] = DateTimeOffset.UtcNow + DismissFor;
        }
    }

    private IReadOnlyList<InsightDto> Rank(IEnumerable<InsightDto> insights, int limit)
    {
        // Min-heap so the score is negated to get the best ones out first
        PriorityQueue<InsightDto, int> ranked = new();

        lock (_gate)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;

            foreach (InsightDto insight in insights)
            {
                if (_dismissedUntil.TryGetValue(insight.Id, out DateTimeOffset until) && until > now)
                {
                    continue;
                }

                ranked.Enqueue(insight, -insight.Score);
            }
        }

        List<InsightDto> top = [];

        while (top.Count < limit && ranked.TryDequeue(out InsightDto? insight, out _))
        {
            top.Add(insight);
        }

        return top;
    }

    private IEnumerable<InsightDto> RulesFor(ActiveErrorDto error)
    {
        DeviceDto? device = devices.Get(error.NodeId);

        if (device is null)
        {
            yield break;
        }

        bool critical = error.Severity == AlertSeverity.Critical;
        int score = critical ? 90 : 65;
        string area = AreaOf(device.Location);
        List<DeviceDto> actuators = [.. devices.ActuatorsAt(device.Location)];
        bool suggested = false;

        switch (error.Kind)
        {
            case AlertKind.MoistureDrop:
                foreach (DeviceDto pump in actuators.Where(a => a.Actuator == ActuatorKind.Pump && !IsOn(a)))
                {
                    suggested = true;
                    yield return Action(CommandType.StartPump, pump.NodeId, error, score, $"{device.NodeId} is {(critical ? "critically dry" : "drying out")} and the pump in {area} is off");
                }

                foreach (DeviceDto valve in actuators.Where(a => a.Actuator == ActuatorKind.Valve && !IsOn(a)))
                {
                    suggested = true;
                    yield return Action(CommandType.OpenValve, valve.NodeId, error, score - 5, $"{device.NodeId} is {(critical ? "critically dry" : "drying out")} and {valve.NodeId} is closed");
                }

                if (!suggested)
                {
                    yield return Look(device.NodeId, error, score - 10, $"Check the drip line at {device.NodeId}", $"Irrigation in {area} is running but {device.NodeId} is still dry");
                }

                break;

            case AlertKind.MoistureFlood:
                foreach (DeviceDto valve in actuators.Where(a => a.Actuator == ActuatorKind.Valve && IsOn(a)))
                {
                    suggested = true;
                    yield return Action(CommandType.CloseValve, valve.NodeId, error, score, $"{device.NodeId} is flooding and {valve.NodeId} is still open");
                }

                // Only worth stopping the pump if there was no valve to close
                if (!suggested)
                {
                    foreach (DeviceDto pump in actuators.Where(a => a.Actuator == ActuatorKind.Pump && IsOn(a)))
                    {
                        suggested = true;
                        yield return Action(CommandType.StopPump, pump.NodeId, error, score - 5, $"{device.NodeId} is flooding and the pump in {area} is still running");
                    }
                }

                if (!suggested)
                {
                    yield return Look(device.NodeId, error, score - 10, $"Check {device.NodeId} for a leak", $"{device.NodeId} reads wet with irrigation in {area} off");
                }

                break;

            case AlertKind.PowerSpike:
                // Cutting power is drastic so only suggest it for a critical spike
                if (critical)
                {
                    foreach (DeviceDto relay in actuators.Where(a => a.Actuator == ActuatorKind.Relay && IsOn(a)))
                    {
                        suggested = true;
                        yield return Action(CommandType.RelayOff, relay.NodeId, error, score, $"{device.NodeId} has a severe power spike, cutting {area} protects the equipment");
                    }
                }

                if (!suggested)
                {
                    yield return Look(device.NodeId, error, score - 10, $"Watch {device.NodeId}", $"{device.NodeId} is drawing more power than normal");
                }

                break;
        }
    }

    private InsightDto DisconnectedInsight(string nodeId)
    {
        int waiting = outbox.PendingFor(nodeId);
        string reason = waiting > 0
            ? $"{nodeId} stopped reporting and has {waiting} command(s) waiting for it"
            : $"{nodeId} stopped reporting, check its power or Wi-Fi";

        return new InsightDto
        {
            Id = LookId(nodeId),
            Kind = InsightKind.Highlight,
            Source = InsightSource.Rule,
            Title = $"Check {nodeId}",
            Reason = reason,
            Score = waiting > 0 ? 80 : 75,
            Severity = AlertSeverity.Critical,
            NodeId = nodeId,
            TriggerNodeId = nodeId
        };
    }

    private InsightDto? LearnedInsight(OperatorActivity trigger, OperatorActivity next, int count, double confidence)
    {
        // Stronger and more often seen patterns score higher, capped so a rule on a critical still wins
        int score = 40 + (int)(confidence * 35) + Math.Min(count, 10);
        string reason = $"After {trigger.Describe()} you usually go for this ({count} times)";

        if (next.Kind == ActivityKind.Command && next.Command is CommandType type)
        {
            // Skip it if the device is already where the command would put it
            if (next.NodeId is not null)
            {
                DeviceDto? target = devices.Get(next.NodeId);

                if (target is null || IsOn(target) == CommandTypes.DesiredState(type))
                {
                    return null;
                }
            }

            return new InsightDto
            {
                Id = ActionId(type, next.NodeId),
                Kind = InsightKind.SuggestedAction,
                Source = InsightSource.Learned,
                Title = Title(type, next.NodeId),
                Reason = reason,
                Score = score,
                NodeId = next.NodeId ?? "ALL",
                TriggerNodeId = trigger.NodeId,
                Command = new IssueCommandDto { NodeId = next.NodeId, Type = type }
            };
        }

        // A free text search can't be pointed at a device so only searches for real nodes count
        if (next.NodeId is null || devices.Get(next.NodeId) is null)
        {
            return null;
        }

        return new InsightDto
        {
            Id = LookId(next.NodeId),
            Kind = InsightKind.Highlight,
            Source = InsightSource.Learned,
            Title = $"Look at {next.NodeId}",
            Reason = reason,
            Score = score - 5,
            NodeId = next.NodeId,
            TriggerNodeId = trigger.NodeId
        };
    }

    // Same id from a rule and from learning means both agree, so keep the stronger one and give it a bump
    private static void Add(Dictionary<string, InsightDto> byId, InsightDto insight)
    {
        if (!byId.TryGetValue(insight.Id, out InsightDto? existing))
        {
            byId[insight.Id] = insight;
            return;
        }

        if (existing.Source == insight.Source)
        {
            if (insight.Score > existing.Score)
            {
                byId[insight.Id] = insight;
            }

            return;
        }

        InsightDto rule = existing.Source == InsightSource.Learned ? insight : existing;
        InsightDto learned = existing.Source == InsightSource.Learned ? existing : insight;

        byId[insight.Id] = rule with
        {
            Source = InsightSource.Combined,
            Score = Math.Min(100, Math.Max(rule.Score, learned.Score) + 5),
            Reason = $"{rule.Reason}. {learned.Reason}"
        };
    }

    private static InsightDto Action(CommandType type, string nodeId, ActiveErrorDto error, int score, string reason) => new()
    {
        Id = ActionId(type, nodeId),
        Kind = InsightKind.SuggestedAction,
        Source = InsightSource.Rule,
        Title = Title(type, nodeId),
        Reason = reason,
        Score = score,
        Severity = error.Severity,
        NodeId = nodeId,
        TriggerNodeId = error.NodeId,
        Command = new IssueCommandDto { NodeId = nodeId, Type = type }
    };

    private static InsightDto Look(string nodeId, ActiveErrorDto error, int score, string title, string reason) => new()
    {
        Id = LookId(nodeId),
        Kind = InsightKind.Highlight,
        Source = InsightSource.Rule,
        Title = title,
        Reason = reason,
        Score = score,
        Severity = error.Severity,
        NodeId = nodeId,
        TriggerNodeId = error.NodeId
    };

    // Commanded state counts as on already, otherwise it would keep suggesting something that has been sent
    private static bool IsOn(DeviceDto device) => device.CommandedState ?? device.IsOn ?? false;

    private static string AreaOf(string location) => location.Split('/')[^1];

    private static string ActionId(CommandType type, string? nodeId) => $"act-{type}-{nodeId ?? "all"}".ToLowerInvariant();

    private static string LookId(string nodeId) => $"look-{nodeId}".ToLowerInvariant();

    private static string Title(CommandType type, string? nodeId) => type switch
    {
        CommandType.StartPump => $"Start {nodeId}",
        CommandType.StopPump => $"Stop {nodeId}",
        CommandType.OpenValve => $"Open {nodeId}",
        CommandType.CloseValve => $"Close {nodeId}",
        CommandType.RelayOn => $"Switch on {nodeId}",
        CommandType.RelayOff => $"Cut power at {nodeId}",
        CommandType.ShutAllValves => "Shut all valves",
        _ => $"{type} {nodeId}"
    };
}
