using System.Net;
using System.Net.Http.Json;
using ArtNet_Dmx_Lights.Models;
using ArtNet_Dmx_Lights.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArtNetDmxLights.Tests;

public sealed class ApiGroupTests
{
    [Fact]
    public async Task GroupCrud_Works()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/v1/groups", new FixtureGroup
        {
            Name = "Front Wash",
            Universe = 0,
            StartChannel = 1,
            ChannelCount = 5
        }, TestJson.Options);

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<FixtureGroup>(TestJson.Options);
        Assert.NotNull(created);

        var listResponse = await client.GetAsync("/api/v1/groups");
        listResponse.EnsureSuccessStatusCode();
        var list = await listResponse.Content.ReadFromJsonAsync<List<FixtureGroup>>(TestJson.Options);
        Assert.NotNull(list);
        Assert.Single(list!);

        var getResponse = await client.GetAsync($"/api/v1/groups/{created!.Id}");
        getResponse.EnsureSuccessStatusCode();

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/groups/{created.Id}", new FixtureGroup
        {
            Name = "Front Wash Updated",
            Universe = 0,
            StartChannel = 1,
            ChannelCount = 6
        }, TestJson.Options);

        updateResponse.EnsureSuccessStatusCode();

        var deleteResponse = await client.DeleteAsync($"/api/v1/groups/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task GroupUpdate_AdjustsPresetValues()
    {
        await using var factory = new TestAppFactory();
        var store = factory.Services.GetRequiredService<IAppStateStore>();
        var client = factory.CreateClient();

        var groupId = Guid.NewGuid();
        var presetId = Guid.NewGuid();

        await store.UpdateAsync(state =>
        {
            state.Groups.Add(new FixtureGroup
            {
                Id = groupId,
                Name = "Front",
                Universe = 0,
                StartChannel = 1,
                ChannelCount = 2
            });

            state.Presets.Add(new Preset
            {
                Id = presetId,
                Name = "Warm",
                FadeMs = 0,
                Groups =
                [
                    new PresetGroup
                    {
                        GroupId = groupId,
                        Values = [1, 2]
                    }
                ]
            });

            return true;
        }, CancellationToken.None);

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/groups/{groupId}", new FixtureGroup
        {
            Name = "Front",
            Universe = 0,
            StartChannel = 1,
            ChannelCount = 3
        }, TestJson.Options);

        updateResponse.EnsureSuccessStatusCode();

        var snapshot = await store.GetSnapshotAsync(CancellationToken.None);
        var preset = snapshot.Presets.Single(p => p.Id == presetId);
        Assert.Equal(3, preset.Groups[0].Values.Length);
        Assert.Equal(0, preset.Groups[0].Values[2]);
    }

    [Fact]
    public async Task GroupDelete_RemovesPresetAndSchedule()
    {
        await using var factory = new TestAppFactory();
        var store = factory.Services.GetRequiredService<IAppStateStore>();
        var client = factory.CreateClient();

        var groupId = Guid.NewGuid();
        var presetId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();

        await store.UpdateAsync(state =>
        {
            state.Groups.Add(new FixtureGroup
            {
                Id = groupId,
                Name = "Front",
                Universe = 0,
                StartChannel = 1,
                ChannelCount = 2
            });

            state.Presets.Add(new Preset
            {
                Id = presetId,
                Name = "Warm",
                FadeMs = 0,
                Groups =
                [
                    new PresetGroup
                    {
                        GroupId = groupId,
                        Values = [1, 2]
                    }
                ]
            });

            state.Schedules.Add(new Schedule
            {
                Id = scheduleId,
                Name = "Morning",
                PresetId = presetId,
                Type = ScheduleType.Fixed,
                Time = "06:30",
                OffsetMinutes = 0,
                Enabled = true,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            return true;
        }, CancellationToken.None);

        var deleteResponse = await client.DeleteAsync($"/api/v1/groups/{groupId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var snapshot = await store.GetSnapshotAsync(CancellationToken.None);
        Assert.Empty(snapshot.Presets);
        Assert.Empty(snapshot.Schedules);
    }
}
