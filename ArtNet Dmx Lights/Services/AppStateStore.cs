using System.Text.Json;
using ArtNet_Dmx_Lights.Models;

namespace ArtNet_Dmx_Lights.Services;

public sealed class AppStateStore : IAppStateStore
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonFileStore _fileStore;
    private readonly string _basePath;
    private AppState _state = new();

    public AppStateStore(JsonSerializerOptions jsonOptions, string basePath)
    {
        _fileStore = new JsonFileStore(jsonOptions);
        _basePath = basePath;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _state = await LoadStateAsync(cancellationToken);
            EnsurePresetOrdering(_state.Presets);
            await SaveStateAsync(_state, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<AppState> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return CloneState(_state);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<T> UpdateAsync<T>(Func<AppState, T> action, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var result = action(_state);
            await SaveStateAsync(_state, cancellationToken);
            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<AppState> LoadStateAsync(CancellationToken cancellationToken)
    {
        var settingsPath = DataPaths.GetSettingsPath(_basePath);
        var groupsPath = DataPaths.GetGroupsPath(_basePath);
        var presetsPath = DataPaths.GetPresetsPath(_basePath);
        var schedulesPath = DataPaths.GetSchedulesPath(_basePath);
        var logsPath = DataPaths.GetLogsPath(_basePath);
        var runtimePath = DataPaths.GetRuntimePath(_basePath);

        var settings = await _fileStore.ReadAsync(settingsPath, new AppSettings(), cancellationToken);
        var groups = await _fileStore.ReadAsync(groupsPath, new List<FixtureGroup>(), cancellationToken);
        var presets = await _fileStore.ReadAsync(presetsPath, new List<Preset>(), cancellationToken);
        var schedules = await _fileStore.ReadAsync(schedulesPath, new List<Schedule>(), cancellationToken);
        var logs = await _fileStore.ReadAsync(logsPath, new List<LogEntry>(), cancellationToken);
        var runtime = await _fileStore.ReadAsync(runtimePath, new RuntimeState(), cancellationToken);

        return new AppState
        {
            Settings = settings,
            Groups = groups,
            Presets = presets,
            Schedules = schedules,
            Logs = logs,
            Runtime = runtime
        };
    }

    private async Task SaveStateAsync(AppState state, CancellationToken cancellationToken)
    {
        var settingsPath = DataPaths.GetSettingsPath(_basePath);
        var groupsPath = DataPaths.GetGroupsPath(_basePath);
        var presetsPath = DataPaths.GetPresetsPath(_basePath);
        var schedulesPath = DataPaths.GetSchedulesPath(_basePath);
        var logsPath = DataPaths.GetLogsPath(_basePath);
        var runtimePath = DataPaths.GetRuntimePath(_basePath);

        await _fileStore.WriteAsync(settingsPath, state.Settings, cancellationToken);
        await _fileStore.WriteAsync(groupsPath, state.Groups, cancellationToken);
        await _fileStore.WriteAsync(presetsPath, state.Presets, cancellationToken);
        await _fileStore.WriteAsync(schedulesPath, state.Schedules, cancellationToken);
        await _fileStore.WriteAsync(logsPath, state.Logs, cancellationToken);
        await _fileStore.WriteAsync(runtimePath, state.Runtime, cancellationToken);
    }

    private static AppState CloneState(AppState source)
    {
        return new AppState
        {
            Settings = CloneSettings(source.Settings),
            Groups = source.Groups.Select(CloneGroup).ToList(),
            Presets = source.Presets.Select(ClonePreset).ToList(),
            Schedules = source.Schedules.Select(CloneSchedule).ToList(),
            Logs = source.Logs.Select(CloneLog).ToList(),
            Runtime = CloneRuntime(source.Runtime)
        };
    }

    private static AppSettings CloneSettings(AppSettings settings)
    {
        return new AppSettings
        {
            ControllerHost = settings.ControllerHost,
            ArtnetPort = settings.ArtnetPort,
            ArtnetNet = settings.ArtnetNet,
            ArtnetSubNet = settings.ArtnetSubNet,
            UniverseBase = settings.UniverseBase,
            ZipCode = settings.ZipCode,
            ResolvedTimeZone = settings.ResolvedTimeZone,
            ResolvedLat = settings.ResolvedLat,
            ResolvedLon = settings.ResolvedLon,
            ManualTimeZone = settings.ManualTimeZone,
            ManualLat = settings.ManualLat,
            ManualLon = settings.ManualLon,
            LastSunriseUtc = settings.LastSunriseUtc,
            LastSunsetUtc = settings.LastSunsetUtc
        };
    }

    private static FixtureGroup CloneGroup(FixtureGroup group)
    {
        return new FixtureGroup
        {
            Id = group.Id,
            Name = group.Name,
            Universe = group.Universe,
            StartChannel = group.StartChannel,
            ChannelCount = group.ChannelCount
        };
    }

    private static Preset ClonePreset(Preset preset)
    {
        return new Preset
        {
            Id = preset.Id,
            Name = preset.Name,
            ListOrder = preset.ListOrder,
            GridLocation = preset.GridLocation,
            FadeMs = preset.FadeMs,
            Groups = preset.Groups.Select(ClonePresetGroup).ToList()
        };
    }

    private static PresetGroup ClonePresetGroup(PresetGroup group)
    {
        return new PresetGroup
        {
            GroupId = group.GroupId,
            Values = group.Values.ToArray()
        };
    }

    private static Schedule CloneSchedule(Schedule schedule)
    {
        return new Schedule
        {
            Id = schedule.Id,
            Name = schedule.Name,
            PresetId = schedule.PresetId,
            Type = schedule.Type,
            Time = schedule.Time,
            OffsetMinutes = schedule.OffsetMinutes,
            Enabled = schedule.Enabled,
            UpdatedAt = schedule.UpdatedAt
        };
    }

    private static LogEntry CloneLog(LogEntry log)
    {
        return new LogEntry
        {
            Id = log.Id,
            TimestampUtc = log.TimestampUtc,
            Type = log.Type,
            PresetId = log.PresetId,
            PresetName = log.PresetName,
            GroupId = log.GroupId,
            GroupName = log.GroupName
        };
    }

    private static RuntimeState CloneRuntime(RuntimeState state)
    {
        return new RuntimeState
        {
            ActivePresetId = state.ActivePresetId,
            ActiveSource = state.ActiveSource,
            LastScheduledAt = state.LastScheduledAt,
            LastScheduledPresetId = state.LastScheduledPresetId
        };
    }

    private static void EnsurePresetOrdering(List<Preset> presets)
    {
        if (presets.Count == 0)
        {
            return;
        }

        var listOrders = presets.Select(p => p.ListOrder).ToList();
        var gridOrders = presets.Select(p => p.GridLocation).ToList();

        var hasInvalidList = listOrders.Any(o => o <= 0) || listOrders.Distinct().Count() != presets.Count;
        var hasInvalidGrid = gridOrders.Any(o => o < 0) || gridOrders.Distinct().Count() != presets.Count;

        if (!hasInvalidList && !hasInvalidGrid)
        {
            return;
        }

        var ordered = presets
            .OrderBy(p => p.ListOrder <= 0 ? int.MaxValue : p.ListOrder)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            if (hasInvalidList)
            {
                ordered[i].ListOrder = i + 1;
            }

            if (hasInvalidGrid)
            {
                ordered[i].GridLocation = i;
            }
        }
    }
}
