using ArtNet_Dmx_Lights.Models;
using ArtNet_Dmx_Lights.Services;
using Xunit;

namespace ArtNetDmxLights.Tests;

public sealed class LogRetentionTests
{
    [Fact]
    public void Trim_RemovesEntriesBeforeCutoff()
    {
        var policy = new LogRetentionPolicy();
        var cutoff = DateTimeOffset.UtcNow.AddHours(-72);
        var logs = new List<LogEntry>
        {
            new LogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = cutoff.AddMinutes(-1),
                Type = LogEventType.DmxChange
            },
            new LogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = cutoff.AddMinutes(1),
                Type = LogEventType.PresetSaved
            }
        };

        var removed = policy.Trim(logs, cutoff);

        Assert.Equal(1, removed);
        Assert.Single(logs);
        Assert.Equal(LogEventType.PresetSaved, logs[0].Type);
    }
}
