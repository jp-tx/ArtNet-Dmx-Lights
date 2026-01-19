namespace ArtNet_Dmx_Lights.Models;

public sealed class FixtureGroup
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Universe { get; set; }
    public int StartChannel { get; set; }
    public int ChannelCount { get; set; }
}
