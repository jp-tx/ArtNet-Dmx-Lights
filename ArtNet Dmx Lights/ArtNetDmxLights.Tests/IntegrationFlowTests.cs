using System.Net.Http.Json;
using System.Text.Json;
using ArtNet_Dmx_Lights.Models;
using ArtNet_Dmx_Lights.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArtNetDmxLights.Tests;

public sealed class IntegrationFlowTests
{
    [Fact]
    public async Task Settings_Groups_Presets_Activate_Flow()
    {
        await using var factory = new PresetTestAppFactory();
        var client = factory.CreateClient();

        var settingsResponse = await client.PutAsJsonAsync("/api/v1/settings", new AppSettings
        {
            ControllerHost = "controller.local",
            ArtnetPort = 6454,
            ArtnetNet = 0,
            ArtnetSubNet = 0,
            UniverseBase = 0,
            ZipCode = "78701"
        }, TestJson.Options);
        settingsResponse.EnsureSuccessStatusCode();

        var groupResponse = await client.PostAsJsonAsync("/api/v1/groups", new FixtureGroup
        {
            Name = "Flow Group",
            Universe = 0,
            StartChannel = 1,
            ChannelCount = 1
        }, TestJson.Options);
        var group = await groupResponse.Content.ReadFromJsonAsync<FixtureGroup>(TestJson.Options);

        var presetResponse = await client.PostAsJsonAsync("/api/v1/presets", new Preset
        {
            Name = "Flow Preset",
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

        var logsResponse = await client.GetAsync("/api/v1/logs?type=DmxChange");
        logsResponse.EnsureSuccessStatusCode();
        var logs = await logsResponse.Content.ReadFromJsonAsync<List<LogEntry>>(TestJson.Options);
        Assert.NotNull(logs);
        Assert.NotEmpty(logs!);
    }

    [Fact]
    public async Task Schedules_Trigger_ActivePreset_Flow()
    {
        await using var factory = new PresetTestAppFactory();
        var client = factory.CreateClient();
        var evaluator = factory.Services.GetRequiredService<ScheduleEvaluator>();
        var engine = factory.Services.GetRequiredService<DmxEngine>();
        var store = factory.Services.GetRequiredService<IAppStateStore>();

        var groupResponse = await client.PostAsJsonAsync("/api/v1/groups", new FixtureGroup
        {
            Name = "Schedule Group",
            Universe = 0,
            StartChannel = 1,
            ChannelCount = 1
        }, TestJson.Options);
        var group = await groupResponse.Content.ReadFromJsonAsync<FixtureGroup>(TestJson.Options);

        var presetResponse = await client.PostAsJsonAsync("/api/v1/presets", new Preset
        {
            Name = "Schedule Preset",
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

        var scheduleResponse = await client.PostAsJsonAsync("/api/v1/schedules", new Schedule
        {
            Name = "Daily",
            PresetId = preset!.Id,
            Type = ScheduleType.Fixed,
            Time = "06:30",
            Enabled = true
        }, TestJson.Options);
        scheduleResponse.EnsureSuccessStatusCode();

        var snapshot = await store.GetSnapshotAsync(CancellationToken.None);
        var nowUtc = new DateTimeOffset(2026, 1, 19, 6, 30, 30, TimeSpan.Zero);
        var due = evaluator.GetDueSchedule(
            snapshot.Schedules,
            TimeZoneInfo.Utc,
            new DateOnly(2026, 1, 19),
            nowUtc,
            DateTimeOffset.MinValue,
            null);

        Assert.NotNull(due);
        await engine.ActivatePresetAsync(due!.PresetId, ActiveSource.Schedule, CancellationToken.None);

        var status = await (await client.GetAsync("/api/v1/status")).Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        Assert.Equal(preset.Id.ToString(), status.GetProperty("activePresetId").GetString());
    }

    [Fact]
    public async Task Backup_Export_Import_Flow()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();

        var groupResponse = await client.PostAsJsonAsync("/api/v1/groups", new FixtureGroup
        {
            Name = "Backup Group",
            Universe = 0,
            StartChannel = 1,
            ChannelCount = 1
        }, TestJson.Options);
        var group = await groupResponse.Content.ReadFromJsonAsync<FixtureGroup>(TestJson.Options);

        var presetResponse = await client.PostAsJsonAsync("/api/v1/presets", new Preset
        {
            Name = "Backup Preset",
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
        presetResponse.EnsureSuccessStatusCode();

        var backupResponse = await client.GetAsync("/api/v1/backup");
        backupResponse.EnsureSuccessStatusCode();

        var bundle = await backupResponse.Content.ReadFromJsonAsync<BackupBundle>(TestJson.Options);
        Assert.NotNull(bundle);
        Assert.Single(bundle!.Groups);

        bundle.Groups = [];
        bundle.Presets = [];
        bundle.Schedules = [];

        var importResponse = await client.PostAsJsonAsync("/api/v1/backup", bundle, TestJson.Options);
        importResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task LoggingRetention_Flow()
    {
        await using var factory = new TestAppFactory();
        var store = factory.Services.GetRequiredService<IAppStateStore>();
        var policy = factory.Services.GetRequiredService<LogRetentionPolicy>();

        var cutoff = DateTimeOffset.UtcNow.AddHours(-72);
        await store.UpdateAsync(state =>
        {
            state.Logs.Add(new LogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = cutoff.AddMinutes(-1),
                Type = LogEventType.DmxChange
            });
            state.Logs.Add(new LogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = cutoff.AddMinutes(1),
                Type = LogEventType.PresetSaved
            });
            policy.Trim(state.Logs, cutoff);
            return true;
        }, CancellationToken.None);

        var snapshot = await store.GetSnapshotAsync(CancellationToken.None);
        Assert.Single(snapshot.Logs);
    }
}
