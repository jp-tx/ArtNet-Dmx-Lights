namespace ArtNet_Dmx_Lights.Models;

public sealed class ArtNetNodeInfo
{
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public string ShortName { get; set; } = string.Empty;
    public string LongName { get; set; } = string.Empty;
}

public sealed class ArtNetDiscoveryResponse
{
    public List<ArtNetNodeInfo> Nodes { get; set; } = [];
    public string? Warning { get; set; }
    public int TimeoutMs { get; set; }
}
