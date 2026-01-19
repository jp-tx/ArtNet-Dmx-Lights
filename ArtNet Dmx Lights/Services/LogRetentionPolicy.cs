using ArtNet_Dmx_Lights.Models;

namespace ArtNet_Dmx_Lights.Services;

public sealed class LogRetentionPolicy
{
    public int Trim(List<LogEntry> logs, DateTimeOffset cutoff)
    {
        return logs.RemoveAll(log => log.TimestampUtc < cutoff);
    }
}
