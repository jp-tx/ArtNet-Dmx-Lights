namespace ArtNet_Dmx_Lights.Models;

public sealed class AppSettings
{
    public string ControllerHost { get; set; } = string.Empty;
    public int ArtnetPort { get; set; } = 6454;
    public int ArtnetNet { get; set; } = 0;
    public int ArtnetSubNet { get; set; } = 0;
    public int UniverseBase { get; set; } = 0;
    public string ZipCode { get; set; } = string.Empty;

    public string ResolvedTimeZone { get; set; } = string.Empty;
    public double? ResolvedLat { get; set; }
    public double? ResolvedLon { get; set; }
    public string ManualTimeZone { get; set; } = string.Empty;
    public double? ManualLat { get; set; }
    public double? ManualLon { get; set; }

    public DateTimeOffset? LastSunriseUtc { get; set; }
    public DateTimeOffset? LastSunsetUtc { get; set; }
}
