using System.Net.Http.Json;
using System.Text.Json;
using ArtNet_Dmx_Lights.Models;
using ArtNet_Dmx_Lights.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArtNetDmxLights.Tests;

public sealed class ApiBackupStatusTests
{
    [Fact]
    public async Task Backup_Import_ReplacesConfig_AndPreservesLogs()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();
        var store = factory.Services.GetRequiredService<IAppStateStore>();

        await store.UpdateAsync(state =>
        {
            state.Logs.Add(new LogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = DateTimeOffset.UtcNow,
                Type = LogEventType.PresetSaved
            });
            return true;
        }, CancellationToken.None);

        var importBundle = new BackupBundle
        {
            Settings = new AppSettings { ControllerHost = "controller.local" },
            Groups = [],
            Presets = [],
            Schedules = []
        };

        var importResponse = await client.PostAsJsonAsync("/api/v1/backup", importBundle, TestJson.Options);
        importResponse.EnsureSuccessStatusCode();

        var snapshot = await store.GetSnapshotAsync(CancellationToken.None);
        Assert.Equal("controller.local", snapshot.Settings.ControllerHost);
        Assert.Single(snapshot.Logs);
    }

    [Fact]
    public async Task Status_ReturnsActivePreset()
    {
        await using var factory = new PresetTestAppFactory();
        var client = factory.CreateClient();

        var groupResponse = await client.PostAsJsonAsync("/api/v1/groups", new FixtureGroup
        {
            Name = "Status Group",
            Universe = 0,
            StartChannel = 1,
            ChannelCount = 1
        }, TestJson.Options);
        var group = await groupResponse.Content.ReadFromJsonAsync<FixtureGroup>(TestJson.Options);

        var presetResponse = await client.PostAsJsonAsync("/api/v1/presets", new Preset
        {
            Name = "Status Preset",
            FadeMs = 0,
            Groups =
            [
                new PresetGroup
                {
                    GroupId = group!.Id,
                    Values = [10]
                }
            ]
        }, TestJson.Options);
        var preset = await presetResponse.Content.ReadFromJsonAsync<Preset>(TestJson.Options);

        var activateResponse = await client.PostAsync($"/api/v1/presets/{preset!.Id}/activate", null);
        activateResponse.EnsureSuccessStatusCode();

        var statusResponse = await client.GetAsync("/api/v1/status");
        statusResponse.EnsureSuccessStatusCode();

        var status = await statusResponse.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        Assert.Equal(preset.Id.ToString(), status.GetProperty("activePresetId").GetString());
        Assert.Equal("manual", status.GetProperty("activeSource").GetString());
    }
}
