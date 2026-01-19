using ArtNet_Dmx_Lights.Models;
using ArtNet_Dmx_Lights.Services;
using Xunit;

namespace ArtNetDmxLights.Tests;

public sealed class ValidationTests
{
    private readonly ValidationService _validator = new();

    [Fact]
    public void Settings_Invalid_WhenMissingHost()
    {
        var settings = new AppSettings
        {
            ControllerHost = "",
            ArtnetPort = 6454,
            ArtnetNet = 0,
            ArtnetSubNet = 0,
            UniverseBase = 0,
            ZipCode = "78701"
        };

        var result = _validator.ValidateSettings(settings);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ControllerHost"));
    }

    [Fact]
    public void Group_Invalid_WhenChannelRangeExceeds512()
    {
        var settings = new AppSettings { UniverseBase = 0 };
        var group = new FixtureGroup
        {
            Name = "Bad",
            Universe = 0,
            StartChannel = 500,
            ChannelCount = 20
        };

        var result = _validator.ValidateGroup(group, settings);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Channel range"));
    }

    [Fact]
    public void Preset_Invalid_WhenValuesOutOfRange()
    {
        var groupId = Guid.NewGuid();
        var groups = new List<FixtureGroup>
        {
            new FixtureGroup
            {
                Id = groupId,
                Name = "Front",
                Universe = 0,
                StartChannel = 1,
                ChannelCount = 2
            }
        };

        var preset = new Preset
        {
            Name = "Bad Preset",
            FadeMs = 0,
            Groups =
            [
                new PresetGroup
                {
                    GroupId = groupId,
                    Values = [0, 300]
                }
            ]
        };

        var result = _validator.ValidatePreset(preset, groups);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("0-255"));
    }

    [Fact]
    public void Schedule_Invalid_WhenTimeMissing()
    {
        var presetId = Guid.NewGuid();
        var schedule = new Schedule
        {
            Name = "Bad",
            PresetId = presetId,
            Type = ScheduleType.Fixed,
            Time = null,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var result = _validator.ValidateSchedule(schedule, new List<Preset>
        {
            new Preset { Id = presetId, Name = "Warm", FadeMs = 0 }
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Time"));
    }

    [Fact]
    public void Settings_Invalid_WhenManualCoordsIncomplete()
    {
        var settings = new AppSettings
        {
            ControllerHost = "controller.local",
            ArtnetPort = 6454,
            ArtnetNet = 0,
            ArtnetSubNet = 0,
            UniverseBase = 0,
            ZipCode = "78701",
            ManualLat = 36.522
        };

        var result = _validator.ValidateSettings(settings);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ManualLat") && e.Contains("ManualLon"));
    }

    [Fact]
    public void Settings_Invalid_WhenManualCoordsOutOfRange()
    {
        var settings = new AppSettings
        {
            ControllerHost = "controller.local",
            ArtnetPort = 6454,
            ArtnetNet = 0,
            ArtnetSubNet = 0,
            UniverseBase = 0,
            ZipCode = "78701",
            ManualLat = 120,
            ManualLon = -200
        };

        var result = _validator.ValidateSettings(settings);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ManualLat"));
        Assert.Contains(result.Errors, e => e.Contains("ManualLon"));
    }
}
