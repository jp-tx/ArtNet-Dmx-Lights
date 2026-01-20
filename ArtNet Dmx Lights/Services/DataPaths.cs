using System;
using System.IO;

namespace ArtNet_Dmx_Lights.Services;

public static class DataPaths
{
    public const string DataDirectoryName = "data";
    public const string SettingsFileName = "settings.json";
    public const string GroupsFileName = "groups.json";
    public const string PresetsFileName = "presets.json";
    public const string SchedulesFileName = "schedules.json";
    public const string LogsFileName = "logs.json";
    public const string RuntimeFileName = "runtime.json";

    public static string GetDataDirectory(string basePath)
    {
        var overridePath = Environment.GetEnvironmentVariable("ARTNET_DATA_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }

        return Path.Combine(basePath, DataDirectoryName);
    }

    public static string GetSettingsPath(string basePath)
        => Path.Combine(GetDataDirectory(basePath), SettingsFileName);

    public static string GetGroupsPath(string basePath)
        => Path.Combine(GetDataDirectory(basePath), GroupsFileName);

    public static string GetPresetsPath(string basePath)
        => Path.Combine(GetDataDirectory(basePath), PresetsFileName);

    public static string GetSchedulesPath(string basePath)
        => Path.Combine(GetDataDirectory(basePath), SchedulesFileName);

    public static string GetLogsPath(string basePath)
        => Path.Combine(GetDataDirectory(basePath), LogsFileName);

    public static string GetRuntimePath(string basePath)
        => Path.Combine(GetDataDirectory(basePath), RuntimeFileName);
}
