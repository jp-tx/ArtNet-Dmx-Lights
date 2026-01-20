using System.Text.Json;
using ArtNet_Dmx_Lights.Models;
using Xunit;

namespace ArtNetDmxLights.Tests;

public sealed class SerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = TestJson.Options;

    [Fact]
    public void Settings_RoundTrip()
    {
        var settings = new AppSettings
        {
            ControllerHost = "controller.local",
            ArtnetPort = 6454,
            ArtnetNet = 1,
            ArtnetSubNet = 2,
            UniverseBase = 1,
            ZipCode = "78701",
            ResolvedTimeZone = "America/Chicago",
            ResolvedLat = 30.27,
            ResolvedLon = -97.74,
            ManualTimeZone = "America/Chicago",
            ManualLat = 36.522,
            ManualLon = -87.349,
            LastSunriseUtc = DateTimeOffset.Parse("2026-01-19T13:00:00Z"),
            LastSunsetUtc = DateTimeOffset.Parse("2026-01-19T23:00:00Z")
        };

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);

        Assert.NotNull(roundTrip);
        Assert.Equal(settings.ControllerHost, roundTrip!.ControllerHost);
        Assert.Equal(settings.ArtnetPort, roundTrip.ArtnetPort);
        Assert.Equal(settings.ArtnetNet, roundTrip.ArtnetNet);
        Assert.Equal(settings.ArtnetSubNet, roundTrip.ArtnetSubNet);
        Assert.Equal(settings.UniverseBase, roundTrip.UniverseBase);
        Assert.Equal(settings.ZipCode, roundTrip.ZipCode);
        Assert.Equal(settings.ResolvedTimeZone, roundTrip.ResolvedTimeZone);
        Assert.Equal(settings.ResolvedLat, roundTrip.ResolvedLat);
        Assert.Equal(settings.ResolvedLon, roundTrip.ResolvedLon);
        Assert.Equal(settings.ManualTimeZone, roundTrip.ManualTimeZone);
        Assert.Equal(settings.ManualLat, roundTrip.ManualLat);
        Assert.Equal(settings.ManualLon, roundTrip.ManualLon);
        Assert.Equal(settings.LastSunriseUtc, roundTrip.LastSunriseUtc);
        Assert.Equal(settings.LastSunsetUtc, roundTrip.LastSunsetUtc);
    }

    [Fact]
    public void Group_RoundTrip()
    {
        var group = new FixtureGroup
        {
            Id = Guid.NewGuid(),
            Name = "Front Wash",
            Universe = 1,
            StartChannel = 1,
            ChannelCount = 5
        };

        var json = JsonSerializer.Serialize(group, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<FixtureGroup>(json, JsonOptions);

        Assert.NotNull(roundTrip);
        Assert.Equal(group.Id, roundTrip!.Id);
        Assert.Equal(group.Name, roundTrip.Name);
        Assert.Equal(group.Universe, roundTrip.Universe);
        Assert.Equal(group.StartChannel, roundTrip.StartChannel);
        Assert.Equal(group.ChannelCount, roundTrip.ChannelCount);
    }

    [Fact]
    public void Preset_RoundTrip()
    {
        var preset = new Preset
        {
            Id = Guid.NewGuid(),
            Name = "Warm",
            ListOrder = 3,
            GridLocation = 5,
            FadeMs = 750,
            Groups =
            [
                new PresetGroup
                {
                    GroupId = Guid.NewGuid(),
                    Values = [0, 128, 255]
                }
            ]
        };

        var json = JsonSerializer.Serialize(preset, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<Preset>(json, JsonOptions);

        Assert.NotNull(roundTrip);
        Assert.Equal(preset.Id, roundTrip!.Id);
        Assert.Equal(preset.Name, roundTrip.Name);
        Assert.Equal(preset.ListOrder, roundTrip.ListOrder);
        Assert.Equal(preset.GridLocation, roundTrip.GridLocation);
        Assert.Equal(preset.FadeMs, roundTrip.FadeMs);
        Assert.Single(roundTrip.Groups);
        Assert.Equal(preset.Groups[0].GroupId, roundTrip.Groups[0].GroupId);
        Assert.Equal(preset.Groups[0].Values, roundTrip.Groups[0].Values);
    }

    [Fact]
    public void Schedule_RoundTrip()
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
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var json = JsonSerializer.Serialize(schedule, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<Schedule>(json, JsonOptions);

        Assert.NotNull(roundTrip);
        Assert.Equal(schedule.Id, roundTrip!.Id);
        Assert.Equal(schedule.Name, roundTrip.Name);
        Assert.Equal(schedule.PresetId, roundTrip.PresetId);
        Assert.Equal(schedule.Type, roundTrip.Type);
        Assert.Equal(schedule.Time, roundTrip.Time);
        Assert.Equal(schedule.OffsetMinutes, roundTrip.OffsetMinutes);
        Assert.Equal(schedule.Enabled, roundTrip.Enabled);
        Assert.Equal(schedule.UpdatedAt, roundTrip.UpdatedAt);
    }

    [Fact]
    public void LogEntry_RoundTrip()
    {
        var log = new LogEntry
        {
            Id = Guid.NewGuid(),
            TimestampUtc = DateTimeOffset.UtcNow,
            Type = LogEventType.DmxChange,
            PresetId = Guid.NewGuid(),
            PresetName = "Warm",
            GroupId = Guid.NewGuid(),
            GroupName = "Front Wash"
        };

        var json = JsonSerializer.Serialize(log, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<LogEntry>(json, JsonOptions);

        Assert.NotNull(roundTrip);
        Assert.Equal(log.Id, roundTrip!.Id);
        Assert.Equal(log.TimestampUtc, roundTrip.TimestampUtc);
        Assert.Equal(log.Type, roundTrip.Type);
        Assert.Equal(log.PresetId, roundTrip.PresetId);
        Assert.Equal(log.PresetName, roundTrip.PresetName);
        Assert.Equal(log.GroupId, roundTrip.GroupId);
        Assert.Equal(log.GroupName, roundTrip.GroupName);
    }

    [Fact]
    public void Backup_RoundTrip()
    {
        var bundle = new BackupBundle
        {
            Settings = new AppSettings { ControllerHost = "controller.local" },
            Groups =
            [
                new FixtureGroup
                {
                    Id = Guid.NewGuid(),
                    Name = "Front Wash",
                    Universe = 1,
                    StartChannel = 1,
                    ChannelCount = 5
                }
            ],
            Presets =
            [
                new Preset
                {
                    Id = Guid.NewGuid(),
                    Name = "Warm",
                    FadeMs = 500,
                    Groups =
                    [
                        new PresetGroup
                        {
                            GroupId = Guid.NewGuid(),
                            Values = [0, 50, 100]
                        }
                    ]
                }
            ],
            Schedules =
            [
                new Schedule
                {
                    Id = Guid.NewGuid(),
                    Name = "Morning",
                    PresetId = Guid.NewGuid(),
                    Type = ScheduleType.Fixed,
                    Time = "06:30",
                    OffsetMinutes = 0,
                    Enabled = true,
                    UpdatedAt = DateTimeOffset.UtcNow
                }
            ]
        };

        var json = JsonSerializer.Serialize(bundle, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<BackupBundle>(json, JsonOptions);

        Assert.NotNull(roundTrip);
        Assert.Equal(bundle.Settings.ControllerHost, roundTrip!.Settings.ControllerHost);
        Assert.Single(roundTrip.Groups);
        Assert.Single(roundTrip.Presets);
        Assert.Single(roundTrip.Schedules);
    }
}
