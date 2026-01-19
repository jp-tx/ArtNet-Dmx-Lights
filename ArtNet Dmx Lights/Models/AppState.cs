namespace ArtNet_Dmx_Lights.Models;

public sealed class AppState
{
    public AppSettings Settings { get; set; } = new();
    public List<FixtureGroup> Groups { get; set; } = [];
    public List<Preset> Presets { get; set; } = [];
    public List<Schedule> Schedules { get; set; } = [];
    public List<LogEntry> Logs { get; set; } = [];
    public RuntimeState Runtime { get; set; } = new();
}
