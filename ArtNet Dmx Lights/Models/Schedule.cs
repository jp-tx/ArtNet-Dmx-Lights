namespace ArtNet_Dmx_Lights.Models;

public enum ScheduleType
{
    Fixed,
    Sunrise,
    Sunset
}

public sealed class Schedule
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid PresetId { get; set; }
    public ScheduleType Type { get; set; }
    public string? Time { get; set; }
    public int OffsetMinutes { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; }
}
