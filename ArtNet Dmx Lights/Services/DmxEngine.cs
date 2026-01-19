using ArtNet_Dmx_Lights.Models;

namespace ArtNet_Dmx_Lights.Services;

public sealed class DmxEngine
{
    private const int UniverseCount = 2;
    private readonly IAppStateStore _store;
    private readonly IArtNetSender _sender;
    private readonly DmxStateCache _stateCache;

    public DmxEngine(IAppStateStore store, IArtNetSender sender, DmxStateCache stateCache)
    {
        _store = store;
        _sender = sender;
        _stateCache = stateCache;
    }

    public async Task<bool> ActivatePresetAsync(Guid presetId, ActiveSource source, CancellationToken cancellationToken)
    {
        var snapshot = await _store.GetSnapshotAsync(cancellationToken);
        var preset = snapshot.Presets.FirstOrDefault(p => p.Id == presetId);
        if (preset is null)
        {
            return false;
        }

        var current = _stateCache.GetSnapshot();
        var target = BuildTarget(snapshot.Settings, snapshot.Groups, preset.Groups, current);

        await ApplyFadeAsync(snapshot.Settings, current, target, preset.FadeMs, cancellationToken);
        _stateCache.SetState(target);

        await _store.UpdateAsync(state =>
        {
            state.Runtime.ActivePresetId = preset.Id;
            state.Runtime.ActiveSource = source;
            if (source == ActiveSource.Schedule)
            {
                state.Runtime.LastScheduledAt = DateTimeOffset.UtcNow;
                state.Runtime.LastScheduledPresetId = preset.Id;
            }

            state.Logs.Add(new LogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = DateTimeOffset.UtcNow,
                Type = LogEventType.DmxChange,
                PresetId = preset.Id,
                PresetName = preset.Name
            });

            return true;
        }, cancellationToken);

        return true;
    }

    public async Task PreviewPresetAsync(Preset preset, CancellationToken cancellationToken)
    {
        var snapshot = await _store.GetSnapshotAsync(cancellationToken);
        var current = _stateCache.GetSnapshot();
        var target = BuildTarget(snapshot.Settings, snapshot.Groups, preset.Groups, current);

        await ApplyFadeAsync(snapshot.Settings, current, target, preset.FadeMs, cancellationToken);
        _stateCache.SetState(target);

        await _store.UpdateAsync(state =>
        {
            state.Runtime.ActivePresetId = preset.Id == Guid.Empty ? null : preset.Id;
            state.Runtime.ActiveSource = ActiveSource.Manual;
            state.Logs.Add(new LogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = DateTimeOffset.UtcNow,
                Type = LogEventType.DmxChange,
                PresetId = preset.Id == Guid.Empty ? null : preset.Id,
                PresetName = preset.Name
            });

            return true;
        }, cancellationToken);
    }

    public async Task ZeroGroupsAsync(AppSettings settings, IReadOnlyList<FixtureGroup> groups, CancellationToken cancellationToken)
    {
        if (groups.Count == 0)
        {
            return;
        }

        var current = _stateCache.GetSnapshot();
        var target = current.Select(values => values.ToArray()).ToArray();

        foreach (var group in groups)
        {
            var universeIndex = group.Universe - settings.UniverseBase;
            if (universeIndex is < 0 or >= UniverseCount)
            {
                continue;
            }

            for (var i = 0; i < group.ChannelCount; i++)
            {
                var channel = group.StartChannel + i;
                if (channel is < 1 or > 512)
                {
                    continue;
                }

                target[universeIndex][channel - 1] = 0;
            }
        }

        await SendFramesAsync(settings, target, cancellationToken);
        _stateCache.SetState(target);

        await _store.UpdateAsync(state =>
        {
            state.Runtime.ActivePresetId = null;
            state.Runtime.ActiveSource = null;
            return true;
        }, cancellationToken);
    }

    private static int[][] BuildTarget(
        AppSettings settings,
        IReadOnlyList<FixtureGroup> groups,
        IReadOnlyList<PresetGroup> presetGroups,
        int[][] current)
    {
        var target = current.Select(values => values.ToArray()).ToArray();
        var groupMap = groups.ToDictionary(g => g.Id);

        var overrides = new int?[UniverseCount][];
        for (var universe = 0; universe < UniverseCount; universe++)
        {
            overrides[universe] = new int?[512];
        }

        foreach (var presetGroup in presetGroups)
        {
            if (!groupMap.TryGetValue(presetGroup.GroupId, out var group))
            {
                continue;
            }

            var universeIndex = group.Universe - settings.UniverseBase;
            if (universeIndex is < 0 or >= UniverseCount)
            {
                continue;
            }

            for (var i = 0; i < presetGroup.Values.Length; i++)
            {
                var channel = group.StartChannel + i;
                if (channel is < 1 or > 512)
                {
                    continue;
                }

                var channelIndex = channel - 1;
                var value = presetGroup.Values[i];
                var existing = overrides[universeIndex][channelIndex];
                overrides[universeIndex][channelIndex] = existing.HasValue ? Math.Max(existing.Value, value) : value;
            }
        }

        for (var universe = 0; universe < UniverseCount; universe++)
        {
            for (var channelIndex = 0; channelIndex < 512; channelIndex++)
            {
                var value = overrides[universe][channelIndex];
                if (value.HasValue)
                {
                    target[universe][channelIndex] = value.Value;
                }
            }
        }

        return target;
    }

    private async Task ApplyFadeAsync(AppSettings settings, int[][] current, int[][] target, int fadeMs, CancellationToken cancellationToken)
    {
        if (fadeMs <= 0)
        {
            await SendFramesAsync(settings, target, cancellationToken);
            return;
        }

        var steps = Math.Max(1, fadeMs / 50);
        var stepDelay = fadeMs / steps;

        for (var step = 1; step <= steps; step++)
        {
            var frame = new int[UniverseCount][];
            for (var universe = 0; universe < UniverseCount; universe++)
            {
                frame[universe] = new int[512];
                for (var channel = 0; channel < 512; channel++)
                {
                    var start = current[universe][channel];
                    var end = target[universe][channel];
                    var value = start + (end - start) * step / steps;
                    frame[universe][channel] = value;
                }
            }

            await SendFramesAsync(settings, frame, cancellationToken);

            if (step < steps)
            {
                await Task.Delay(stepDelay, cancellationToken);
            }
        }
    }

    private async Task SendFramesAsync(AppSettings settings, int[][] frames, CancellationToken cancellationToken)
    {
        for (var universe = 0; universe < UniverseCount; universe++)
        {
            var payload = new byte[512];
            for (var i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)frames[universe][i];
            }

            var universeNumber = universe + settings.UniverseBase;
            await _sender.SendAsync(settings, universeNumber, payload, cancellationToken);
        }
    }
}
