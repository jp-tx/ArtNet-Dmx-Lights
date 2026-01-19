using System.Globalization;
using ArtNet_Dmx_Lights.Models;

namespace ArtNet_Dmx_Lights.Services;

public sealed class ScheduleEvaluator
{
    public Schedule? GetDueSchedule(
        IReadOnlyList<Schedule> schedules,
        TimeZoneInfo timeZone,
        DateOnly date,
        DateTimeOffset nowUtc,
        DateTimeOffset lastScheduledAt,
        SunTimes? sunTimes)
    {
        var dueSchedules = new List<(Schedule Schedule, DateTimeOffset TriggerUtc)>();

        foreach (var schedule in schedules.Where(s => s.Enabled))
        {
            var triggerUtc = schedule.Type switch
            {
                ScheduleType.Fixed => ResolveFixedTime(schedule, timeZone, date),
                ScheduleType.Sunrise => sunTimes?.SunriseUtc.AddMinutes(schedule.OffsetMinutes),
                ScheduleType.Sunset => sunTimes?.SunsetUtc.AddMinutes(schedule.OffsetMinutes),
                _ => null
            };

            if (triggerUtc is null)
            {
                continue;
            }

            var trigger = triggerUtc.Value;
            if (trigger <= lastScheduledAt)
            {
                continue;
            }

            if (trigger <= nowUtc && trigger > nowUtc.AddMinutes(-1))
            {
                dueSchedules.Add((schedule, trigger));
            }
        }

        if (dueSchedules.Count == 0)
        {
            return null;
        }

        return dueSchedules
            .OrderByDescending(s => s.TriggerUtc)
            .ThenByDescending(s => s.Schedule.UpdatedAt)
            .First()
            .Schedule;
    }

    private static DateTimeOffset? ResolveFixedTime(Schedule schedule, TimeZoneInfo timeZone, DateOnly date)
    {
        if (string.IsNullOrWhiteSpace(schedule.Time))
        {
            return null;
        }

        if (!TimeOnly.TryParseExact(schedule.Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return null;
        }

        var localDateTime = date.ToDateTime(time);
        var localOffset = timeZone.GetUtcOffset(localDateTime);
        return new DateTimeOffset(localDateTime, localOffset).ToUniversalTime();
    }
}
