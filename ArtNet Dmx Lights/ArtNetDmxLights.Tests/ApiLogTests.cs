using System.Net.Http.Json;
using ArtNet_Dmx_Lights.Models;
using ArtNet_Dmx_Lights.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArtNetDmxLights.Tests;

public sealed class ApiLogTests
{
    [Fact]
    public async Task LogFilters_Work()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();
        var store = factory.Services.GetRequiredService<IAppStateStore>();

        var presetId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await store.UpdateAsync(state =>
        {
            state.Logs.Clear();
            state.Logs.AddRange(new[]
            {
                new LogEntry
                {
                    Id = Guid.NewGuid(),
                    TimestampUtc = now.AddMinutes(-10),
                    Type = LogEventType.DmxChange,
                    PresetId = presetId,
                    GroupId = groupId
                },
                new LogEntry
                {
                    Id = Guid.NewGuid(),
                    TimestampUtc = now.AddMinutes(-5),
                    Type = LogEventType.PresetSaved
                }
            });
            return true;
        }, CancellationToken.None);

        var response = await client.GetAsync($"/api/v1/logs?type=DmxChange&presetId={presetId}&groupId={groupId}");
        response.EnsureSuccessStatusCode();

        var logs = await response.Content.ReadFromJsonAsync<List<LogEntry>>(TestJson.Options);
        Assert.NotNull(logs);
        Assert.Single(logs!);

        var from = now.AddMinutes(-6).ToString("O");
        var to = now.AddMinutes(-4).ToString("O");
        var rangeResponse = await client.GetAsync($"/api/v1/logs?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}");
        rangeResponse.EnsureSuccessStatusCode();

        var rangeLogs = await rangeResponse.Content.ReadFromJsonAsync<List<LogEntry>>(TestJson.Options);
        Assert.NotNull(rangeLogs);
        Assert.Single(rangeLogs!);
        Assert.Equal(LogEventType.PresetSaved, rangeLogs[0].Type);
    }
}
