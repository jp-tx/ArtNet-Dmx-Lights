using ArtNet_Dmx_Lights.Models;
using ArtNet_Dmx_Lights.Services;
using Xunit;

namespace ArtNetDmxLights.Tests;

public sealed class ScheduleEvaluatorTests
{
    private readonly ScheduleEvaluator _evaluator = new();

    [Fact]
    public void FixedSchedule_IsDue()
    {
        var schedule = new Schedule
        {
            Id = Guid.NewGuid(),
            Name = "Morning",
            PresetId = Guid.NewGuid(),
            Type = ScheduleType.Fixed,
            Time = "06:30",
            OffsetMinutes = 0,
            Enabled = true,
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        var date = new DateOnly(2026, 1, 19);
        var nowUtc = new DateTimeOffset(2026, 1, 19, 6, 30, 30, TimeSpan.Zero);
        var due = _evaluator.GetDueSchedule(
            new[] { schedule },
            TimeZoneInfo.Utc,
            date,
            nowUtc,
            DateTimeOffset.MinValue,
            null);

        Assert.NotNull(due);
        Assert.Equal(schedule.Id, due!.Id);
    }

    [Fact]
    public void SunriseOffset_IsApplied()
    {
        var schedule = new Schedule
        {
            Id = Guid.NewGuid(),
            Name = "Sunrise",
            PresetId = Guid.NewGuid(),
            Type = ScheduleType.Sunrise,
            OffsetMinutes = 10,
            Enabled = true,
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        var date = new DateOnly(2026, 1, 19);
        var sunTimes = new SunTimes(
            new DateTimeOffset(2026, 1, 19, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 19, 23, 0, 0, TimeSpan.Zero));

        var nowUtc = new DateTimeOffset(2026, 1, 19, 12, 10, 30, TimeSpan.Zero);
        var due = _evaluator.GetDueSchedule(
            new[] { schedule },
            TimeZoneInfo.Utc,
            date,
            nowUtc,
            DateTimeOffset.MinValue,
            sunTimes);

        Assert.NotNull(due);
        Assert.Equal(schedule.Id, due!.Id);
    }

    [Fact]
    public void ConflictResolution_UsesLatestUpdatedAt()
    {
        var scheduleA = new Schedule
        {
            Id = Guid.NewGuid(),
            Name = "A",
            PresetId = Guid.NewGuid(),
            Type = ScheduleType.Fixed,
            Time = "06:30",
            Enabled = true,
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };
        var scheduleB = new Schedule
        {
            Id = Guid.NewGuid(),
            Name = "B",
            PresetId = Guid.NewGuid(),
            Type = ScheduleType.Fixed,
            Time = "06:30",
            Enabled = true,
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        var date = new DateOnly(2026, 1, 19);
        var nowUtc = new DateTimeOffset(2026, 1, 19, 6, 30, 30, TimeSpan.Zero);
        var due = _evaluator.GetDueSchedule(
            new[] { scheduleA, scheduleB },
            TimeZoneInfo.Utc,
            date,
            nowUtc,
            DateTimeOffset.MinValue,
            null);

        Assert.NotNull(due);
        Assert.Equal(scheduleB.Id, due!.Id);
    }
}
