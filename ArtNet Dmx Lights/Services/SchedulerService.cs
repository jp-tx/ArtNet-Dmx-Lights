using ArtNet_Dmx_Lights.Models;

namespace ArtNet_Dmx_Lights.Services;

public sealed class SchedulerService : BackgroundService
{
    private readonly IAppStateStore _store;
    private readonly DmxEngine _engine;
    private readonly SunriseSunsetService _sunService;
    private readonly ScheduleEvaluator _evaluator;

    public SchedulerService(IAppStateStore store, DmxEngine engine, SunriseSunsetService sunService, ScheduleEvaluator evaluator)
    {
        _store = store;
        _engine = engine;
        _sunService = sunService;
        _evaluator = evaluator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await EvaluateAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task EvaluateAsync(CancellationToken cancellationToken)
    {
        var snapshot = await _store.GetSnapshotAsync(cancellationToken);
        var settings = snapshot.Settings;

        var timeZoneId = string.IsNullOrWhiteSpace(settings.ManualTimeZone)
            ? settings.ResolvedTimeZone
            : settings.ManualTimeZone;

        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return;
        }

        TimeZoneInfo? timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return;
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTime(nowUtc, timeZone);
        var lastScheduledAt = snapshot.Runtime.LastScheduledAt ?? DateTimeOffset.MinValue;

        var (lat, lon) = GetEffectiveCoordinates(settings);
        var sunTimes = await ResolveSunTimesAsync(settings, lat, lon, DateOnly.FromDateTime(nowLocal.DateTime), cancellationToken);
        var candidate = _evaluator.GetDueSchedule(
            snapshot.Schedules,
            timeZone,
            DateOnly.FromDateTime(nowLocal.DateTime),
            nowUtc,
            lastScheduledAt,
            sunTimes);
        if (candidate is null)
        {
            return;
        }

        if (candidate.Type is ScheduleType.Sunrise or ScheduleType.Sunset)
        {
            await _store.UpdateAsync(state =>
            {
                state.Logs.Add(new LogEntry
                {
                    Id = Guid.NewGuid(),
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Type = candidate.Type == ScheduleType.Sunrise ? LogEventType.SunriseTick : LogEventType.SunsetTick
                });

                return true;
            }, cancellationToken);
        }

        await _engine.ActivatePresetAsync(candidate.PresetId, ActiveSource.Schedule, cancellationToken);
    }

    private async Task<SunTimes?> ResolveSunTimesAsync(AppSettings settings, double? lat, double? lon, DateOnly date, CancellationToken cancellationToken)
    {
        if (!lat.HasValue || !lon.HasValue)
        {
            return null;
        }

        if (settings.LastSunriseUtc.HasValue && settings.LastSunsetUtc.HasValue)
        {
            var cachedDate = DateOnly.FromDateTime(settings.LastSunriseUtc.Value.UtcDateTime);
            if (cachedDate == date)
            {
                return new SunTimes(settings.LastSunriseUtc.Value, settings.LastSunsetUtc.Value);
            }
        }

        var sunTimes = await _sunService.GetAsync(lat.Value, lon.Value, date, cancellationToken);
        if (sunTimes is not null)
        {
            await _store.UpdateAsync(state =>
            {
                state.Settings.LastSunriseUtc = sunTimes.Value.SunriseUtc;
                state.Settings.LastSunsetUtc = sunTimes.Value.SunsetUtc;
                return true;
            }, cancellationToken);
            return sunTimes;
        }

        if (settings.LastSunriseUtc.HasValue && settings.LastSunsetUtc.HasValue)
        {
            return new SunTimes(settings.LastSunriseUtc.Value, settings.LastSunsetUtc.Value);
        }

        return null;
    }

    private static (double? Lat, double? Lon) GetEffectiveCoordinates(AppSettings settings)
    {
        if (settings.ManualLat.HasValue && settings.ManualLon.HasValue)
        {
            return (settings.ManualLat, settings.ManualLon);
        }

        return (settings.ResolvedLat, settings.ResolvedLon);
    }
}
