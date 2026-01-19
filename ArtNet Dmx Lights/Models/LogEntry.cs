namespace ArtNet_Dmx_Lights.Models;

public enum LogEventType
{
    DmxChange,
    PresetSaved,
    PresetUpdated,
    PresetDeleted,
    SunriseTick,
    SunsetTick
}

public sealed class LogEntry
{
    public Guid Id { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
    public LogEventType Type { get; set; }
    public Guid? PresetId { get; set; }
    public string? PresetName { get; set; }
    public Guid? GroupId { get; set; }
    public string? GroupName { get; set; }
}
