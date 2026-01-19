namespace ArtNet_Dmx_Lights.Models;

public sealed class Preset
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int FadeMs { get; set; }
    public List<PresetGroup> Groups { get; set; } = [];
}

public sealed class PresetGroup
{
    public Guid GroupId { get; set; }
    public int[] Values { get; set; } = [];
}
