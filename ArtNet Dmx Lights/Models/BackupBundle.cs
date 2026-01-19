namespace ArtNet_Dmx_Lights.Models;

public sealed class BackupBundle
{
    public AppSettings Settings { get; set; } = new();
    public List<FixtureGroup> Groups { get; set; } = [];
    public List<Preset> Presets { get; set; } = [];
    public List<Schedule> Schedules { get; set; } = [];
}
