using System.Net;
using System.Net.Http.Json;
using ArtNet_Dmx_Lights.Models;
using Xunit;

namespace ArtNetDmxLights.Tests;

public sealed class ApiScheduleTests
{
    [Fact]
    public async Task ScheduleCrud_Works()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();

        var groupResponse = await client.PostAsJsonAsync("/api/v1/groups", new FixtureGroup
        {
            Name = "Front",
            Universe = 0,
            StartChannel = 1,
            ChannelCount = 1
        }, TestJson.Options);
        var group = await groupResponse.Content.ReadFromJsonAsync<FixtureGroup>(TestJson.Options);

        var presetResponse = await client.PostAsJsonAsync("/api/v1/presets", new Preset
        {
            Name = "Warm",
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

        var createResponse = await client.PostAsJsonAsync("/api/v1/schedules", new Schedule
        {
            Name = "Morning",
            PresetId = preset!.Id,
            Type = ScheduleType.Fixed,
            Time = "06:30",
            OffsetMinutes = 0,
            Enabled = true
        }, TestJson.Options);

        createResponse.EnsureSuccessStatusCode();
        var schedule = await createResponse.Content.ReadFromJsonAsync<Schedule>(TestJson.Options);
        Assert.NotNull(schedule);

        var listResponse = await client.GetAsync("/api/v1/schedules");
        listResponse.EnsureSuccessStatusCode();
        var list = await listResponse.Content.ReadFromJsonAsync<List<Schedule>>(TestJson.Options);
        Assert.NotNull(list);
        Assert.Single(list!);

        var getResponse = await client.GetAsync($"/api/v1/schedules/{schedule!.Id}");
        getResponse.EnsureSuccessStatusCode();

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/schedules/{schedule.Id}", new Schedule
        {
            Name = "Morning Updated",
            PresetId = preset.Id,
            Type = ScheduleType.Fixed,
            Time = "07:00",
            OffsetMinutes = 0,
            Enabled = true
        }, TestJson.Options);
        updateResponse.EnsureSuccessStatusCode();

        var deleteResponse = await client.DeleteAsync($"/api/v1/schedules/{schedule.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }
}
