using System.Globalization;
using System.Text.RegularExpressions;
using ArtNet_Dmx_Lights.Models;

namespace ArtNet_Dmx_Lights.Services;

public sealed class ValidationService
{
    private const int UniverseCount = 2;
    private static readonly Regex ZipRegex = new(@"^\d{5}$", RegexOptions.Compiled);

    public ValidationResult ValidateSettings(AppSettings settings)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(settings.ControllerHost))
        {
            result.Errors.Add("ControllerHost is required.");
        }

        if (settings.ArtnetPort is < 1 or > 65535)
        {
            result.Errors.Add("ArtnetPort must be between 1 and 65535.");
        }

        if (settings.ArtnetNet is < 0 or > 127)
        {
            result.Errors.Add("ArtnetNet must be between 0 and 127.");
        }

        if (settings.ArtnetSubNet is < 0 or > 15)
        {
            result.Errors.Add("ArtnetSubNet must be between 0 and 15.");
        }

        if (settings.UniverseBase is not (0 or 1))
        {
            result.Errors.Add("UniverseBase must be 0 or 1.");
        }

        if (!string.IsNullOrWhiteSpace(settings.ZipCode) && !ZipRegex.IsMatch(settings.ZipCode))
        {
            result.Errors.Add("ZipCode must be a 5-digit US ZIP code.");
        }

        if (settings.ManualLat.HasValue ^ settings.ManualLon.HasValue)
        {
            result.Errors.Add("ManualLat and ManualLon must both be set.");
        }

        if (settings.ManualLat.HasValue &&
            (settings.ManualLat.Value < -90 || settings.ManualLat.Value > 90))
        {
            result.Errors.Add("ManualLat must be between -90 and 90.");
        }

        if (settings.ManualLon.HasValue &&
            (settings.ManualLon.Value < -180 || settings.ManualLon.Value > 180))
        {
            result.Errors.Add("ManualLon must be between -180 and 180.");
        }

        return result;
    }

    public ValidationResult ValidateGroup(FixtureGroup group, AppSettings settings)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(group.Name))
        {
            result.Errors.Add("Group name is required.");
        }

        if (!IsUniverseValid(group.Universe, settings.UniverseBase))
        {
            result.Errors.Add("Universe is out of range for current universe base.");
        }

        if (group.ChannelCount is < 1 or > 512)
        {
            result.Errors.Add("ChannelCount must be between 1 and 512.");
        }

        if (group.StartChannel is < 1 or > 512)
        {
            result.Errors.Add("StartChannel must be between 1 and 512.");
        }

        if (group.StartChannel + group.ChannelCount - 1 > 512)
        {
            result.Errors.Add("Channel range exceeds 512.");
        }

        return result;
    }

    public ValidationResult ValidatePreset(Preset preset, IReadOnlyList<FixtureGroup> groups)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(preset.Name))
        {
            result.Errors.Add("Preset name is required.");
        }

        if (preset.FadeMs < 0)
        {
            result.Errors.Add("FadeMs must be 0 or greater.");
        }

        if (preset.Groups.Count == 0)
        {
            result.Errors.Add("Preset must include at least one group.");
        }

        foreach (var presetGroup in preset.Groups)
        {
            var group = groups.FirstOrDefault(g => g.Id == presetGroup.GroupId);
            if (group is null)
            {
                result.Errors.Add($"Preset references missing group {presetGroup.GroupId}.");
                continue;
            }

            if (presetGroup.Values is null)
            {
                result.Errors.Add($"Preset group {group.Name} values are required.");
                continue;
            }

            if (presetGroup.Values.Length != group.ChannelCount)
            {
                result.Errors.Add($"Preset group {group.Name} values length must match channel count.");
            }

            for (var i = 0; i < presetGroup.Values.Length; i++)
            {
                var value = presetGroup.Values[i];
                if (value is < 0 or > 255)
                {
                    result.Errors.Add($"Preset group {group.Name} value at index {i} must be 0-255.");
                    break;
                }
            }
        }

        return result;
    }

    public ValidationResult ValidateSchedule(Schedule schedule, IReadOnlyList<Preset> presets)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(schedule.Name))
        {
            result.Errors.Add("Schedule name is required.");
        }

        if (!presets.Any(p => p.Id == schedule.PresetId))
        {
            result.Errors.Add("Schedule references missing preset.");
        }

        if (schedule.Type == ScheduleType.Fixed)
        {
            if (string.IsNullOrWhiteSpace(schedule.Time))
            {
                result.Errors.Add("Fixed schedule requires Time.");
            }
            else if (!TimeOnly.TryParseExact(schedule.Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                result.Errors.Add("Time must be in HH:mm format.");
            }
        }

        return result;
    }

    private static bool IsUniverseValid(int universe, int universeBase)
    {
        var min = universeBase;
        var max = universeBase + UniverseCount - 1;
        return universe >= min && universe <= max;
    }
}
