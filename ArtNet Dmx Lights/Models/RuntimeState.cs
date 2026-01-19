namespace ArtNet_Dmx_Lights.Models;

public enum ActiveSource
{
    Manual,
    Schedule
}

public sealed class RuntimeState
{
    public Guid? ActivePresetId { get; set; }
    public ActiveSource? ActiveSource { get; set; }
    public DateTimeOffset? LastScheduledAt { get; set; }
    public Guid? LastScheduledPresetId { get; set; }
}
