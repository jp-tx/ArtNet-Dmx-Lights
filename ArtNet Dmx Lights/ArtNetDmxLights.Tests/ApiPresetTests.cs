using System.Net;
using System.Net.Http.Json;
using ArtNet_Dmx_Lights.Models;
using ArtNet_Dmx_Lights.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ArtNetDmxLights.Tests;

public sealed class ApiPresetTests
{
    [Fact]
    public async Task PresetCrud_Works()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();

        var groupResponse = await client.PostAsJsonAsync("/api/v1/groups", new FixtureGroup
        {
            Name = "Front Wash",
            Universe = 0,
            StartChannel = 1,
            ChannelCount = 2
        }, TestJson.Options);
        groupResponse.EnsureSuccessStatusCode();
        var group = await groupResponse.Content.ReadFromJsonAsync<FixtureGroup>(TestJson.Options);

        var createResponse = await client.PostAsJsonAsync("/api/v1/presets", new Preset
        {
            Name = "Warm",
            FadeMs = 0,
            Groups =
            [
                new PresetGroup
                {
                    GroupId = group!.Id,
                    Values = [10, 20]
                }
            ]
        }, TestJson.Options);

        createResponse.EnsureSuccessStatusCode();
        var preset = await createResponse.Content.ReadFromJsonAsync<Preset>(TestJson.Options);
        Assert.NotNull(preset);

        var listResponse = await client.GetAsync("/api/v1/presets");
        listResponse.EnsureSuccessStatusCode();
        var list = await listResponse.Content.ReadFromJsonAsync<List<Preset>>(TestJson.Options);
        Assert.NotNull(list);
        Assert.Single(list!);

        var getResponse = await client.GetAsync($"/api/v1/presets/{preset!.Id}");
        getResponse.EnsureSuccessStatusCode();

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/presets/{preset.Id}", new Preset
        {
            Name = "Warm Updated",
            FadeMs = 100,
            Groups =
            [
                new PresetGroup
                {
                    GroupId = group.Id,
                    Values = [30, 40]
                }
            ]
        }, TestJson.Options);
        updateResponse.EnsureSuccessStatusCode();

        var deleteResponse = await client.DeleteAsync($"/api/v1/presets/{preset.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task ActivatePreset_UsesHighestOverlapAndLogsOnce()
    {
        await using var factory = new PresetTestAppFactory();
        var client = factory.CreateClient();
        var sender = factory.Services.GetRequiredService<IArtNetSender>() as TestArtNetSender;
        var store = factory.Services.GetRequiredService<IAppStateStore>();

        var group1Response = await client.PostAsJsonAsync("/api/v1/groups", new FixtureGroup
        {
            Name = "Group 1",
            Universe = 0,
            StartChannel = 1,
            ChannelCount = 2
        }, TestJson.Options);
        var group2Response = await client.PostAsJsonAsync("/api/v1/groups", new FixtureGroup
        {
            Name = "Group 2",
            Universe = 0,
            StartChannel = 2,
            ChannelCount = 2
        }, TestJson.Options);

        var group1 = await group1Response.Content.ReadFromJsonAsync<FixtureGroup>(TestJson.Options);
        var group2 = await group2Response.Content.ReadFromJsonAsync<FixtureGroup>(TestJson.Options);

        var presetResponse = await client.PostAsJsonAsync("/api/v1/presets", new Preset
        {
            Name = "Overlap",
            FadeMs = 0,
            Groups =
            [
                new PresetGroup
                {
                    GroupId = group1!.Id,
                    Values = [10, 20]
                },
                new PresetGroup
                {
                    GroupId = group2!.Id,
                    Values = [30, 40]
                }
            ]
        }, TestJson.Options);

        var preset = await presetResponse.Content.ReadFromJsonAsync<Preset>(TestJson.Options);
        var activateResponse = await client.PostAsync($"/api/v1/presets/{preset!.Id}/activate", null);
        activateResponse.EnsureSuccessStatusCode();

        var frames = sender!.Frames;
        Assert.NotEmpty(frames);
        var universe0 = frames.Last(f => f.Universe == 0);
        Assert.Equal(10, universe0.Data[0]);
        Assert.Equal(30, universe0.Data[1]);
        Assert.Equal(40, universe0.Data[2]);

        var logs = (await store.GetSnapshotAsync(CancellationToken.None)).Logs;
        Assert.Single(logs, l => l.Type == LogEventType.DmxChange);
    }

    [Fact]
    public async Task ActivatePreset_FadesLinearly()
    {
        await using var factory = new PresetTestAppFactory();
        var client = factory.CreateClient();
        var sender = factory.Services.GetRequiredService<IArtNetSender>() as TestArtNetSender;

        var groupResponse = await client.PostAsJsonAsync("/api/v1/groups", new FixtureGroup
        {
            Name = "Fade Group",
            Universe = 0,
            StartChannel = 1,
            ChannelCount = 1
        }, TestJson.Options);
        var group = await groupResponse.Content.ReadFromJsonAsync<FixtureGroup>(TestJson.Options);

        var presetResponse = await client.PostAsJsonAsync("/api/v1/presets", new Preset
        {
            Name = "Fade",
            FadeMs = 100,
            Groups =
            [
                new PresetGroup
                {
                    GroupId = group!.Id,
                    Values = [100]
                }
            ]
        }, TestJson.Options);

        var preset = await presetResponse.Content.ReadFromJsonAsync<Preset>(TestJson.Options);
        var activateResponse = await client.PostAsync($"/api/v1/presets/{preset!.Id}/activate", null);
        activateResponse.EnsureSuccessStatusCode();

        var universeFrames = sender!.Frames.Where(f => f.Universe == 0).ToList();
        Assert.True(universeFrames.Count >= 2);
        Assert.Equal(50, universeFrames[0].Data[0]);
        Assert.Equal(100, universeFrames[^1].Data[0]);
    }

    [Fact]
    public async Task MovePreset_SwapsOrder()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();

        var groupResponse = await client.PostAsJsonAsync("/api/v1/groups", new FixtureGroup
        {
            Name = "Front Wash",
            Universe = 0,
            StartChannel = 1,
            ChannelCount = 1
        }, TestJson.Options);
        var group = await groupResponse.Content.ReadFromJsonAsync<FixtureGroup>(TestJson.Options);

        await client.PostAsJsonAsync("/api/v1/presets", new Preset
        {
            Name = "First",
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

        var secondResponse = await client.PostAsJsonAsync("/api/v1/presets", new Preset
        {
            Name = "Second",
            FadeMs = 0,
            Groups =
            [
                new PresetGroup
                {
                    GroupId = group!.Id,
                    Values = [20]
                }
            ]
        }, TestJson.Options);
        var second = await secondResponse.Content.ReadFromJsonAsync<Preset>(TestJson.Options);

        var moveResponse = await client.PostAsync($"/api/v1/presets/{second!.Id}/move?direction=up", null);
        moveResponse.EnsureSuccessStatusCode();

        var listResponse = await client.GetAsync("/api/v1/presets");
        listResponse.EnsureSuccessStatusCode();
        var list = await listResponse.Content.ReadFromJsonAsync<List<Preset>>(TestJson.Options);

        Assert.NotNull(list);
        Assert.Equal("Second", list![0].Name);
        Assert.Equal("First", list[1].Name);
    }

    [Fact]
    public async Task MovePreset_UpdatesGridLocations()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();
        var store = factory.Services.GetRequiredService<IAppStateStore>();

        var groupResponse = await client.PostAsJsonAsync("/api/v1/groups", new FixtureGroup
        {
            Name = "Grid",
            Universe = 0,
            StartChannel = 1,
            ChannelCount = 1
        }, TestJson.Options);
        var group = await groupResponse.Content.ReadFromJsonAsync<FixtureGroup>(TestJson.Options);

        var firstResponse = await client.PostAsJsonAsync("/api/v1/presets", new Preset
        {
            Name = "One",
            FadeMs = 0,
            Groups =
            [
                new PresetGroup
                {
                    GroupId = group!.Id,
                    Values = [1]
                }
            ]
        }, TestJson.Options);

        var secondResponse = await client.PostAsJsonAsync("/api/v1/presets", new Preset
        {
            Name = "Two",
            FadeMs = 0,
            Groups =
            [
                new PresetGroup
                {
                    GroupId = group!.Id,
                    Values = [2]
                }
            ]
        }, TestJson.Options);

        var thirdResponse = await client.PostAsJsonAsync("/api/v1/presets", new Preset
        {
            Name = "Three",
            FadeMs = 0,
            Groups =
            [
                new PresetGroup
                {
                    GroupId = group!.Id,
                    Values = [3]
                }
            ]
        }, TestJson.Options);

        var second = await secondResponse.Content.ReadFromJsonAsync<Preset>(TestJson.Options);

        var moveResponse = await client.PostAsync($"/api/v1/presets/{second!.Id}/grid?targetIndex=0", null);
        moveResponse.EnsureSuccessStatusCode();

        var snapshot = await store.GetSnapshotAsync(CancellationToken.None);
        var ordered = snapshot.Presets.OrderBy(p => p.GridLocation).ToList();

        Assert.Equal("Two", ordered[0].Name);
        Assert.Equal(0, ordered[0].GridLocation);
        Assert.Equal("One", ordered[1].Name);
        Assert.Equal(1, ordered[1].GridLocation);
        Assert.Equal("Three", ordered[2].Name);
        Assert.Equal(2, ordered[2].GridLocation);
    }

    [Fact]
    public async Task FixOrder_NormalizesListAndGrid()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();
        var store = factory.Services.GetRequiredService<IAppStateStore>();

        var groupResponse = await client.PostAsJsonAsync("/api/v1/groups", new FixtureGroup
        {
            Name = "Fix",
            Universe = 0,
            StartChannel = 1,
            ChannelCount = 1
        }, TestJson.Options);
        var group = await groupResponse.Content.ReadFromJsonAsync<FixtureGroup>(TestJson.Options);

        var presetResponse = await client.PostAsJsonAsync("/api/v1/presets", new Preset
        {
            Name = "Alpha",
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

        await store.UpdateAsync(state =>
        {
            var existing = state.Presets.First(p => p.Id == preset!.Id);
            existing.ListOrder = 99;
            existing.GridLocation = 99;
            state.Presets.Add(new Preset
            {
                Id = Guid.NewGuid(),
                Name = "Beta",
                ListOrder = 2,
                GridLocation = 5,
                FadeMs = 0,
                Groups =
                [
                    new PresetGroup
                    {
                        GroupId = group.Id,
                        Values = [20]
                    }
                ]
            });
            return true;
        }, CancellationToken.None);

        var fixResponse = await client.PostAsync("/api/v1/presets/fix-order", null);
        fixResponse.EnsureSuccessStatusCode();

        var snapshot = await store.GetSnapshotAsync(CancellationToken.None);
        var listOrders = snapshot.Presets.OrderBy(p => p.ListOrder).Select(p => p.ListOrder).ToList();
        var gridOrders = snapshot.Presets.OrderBy(p => p.GridLocation).Select(p => p.GridLocation).ToList();

        Assert.Equal([1, 2], listOrders);
        Assert.Equal([0, 1], gridOrders);
    }
}

public sealed class PresetTestAppFactory : TestAppFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IArtNetSender>();
            services.AddSingleton<IArtNetSender>(new TestArtNetSender());
        });
    }
}

public sealed class TestArtNetSender : IArtNetSender
{
    public List<(int Universe, byte[] Data)> Frames { get; } = [];

    public Task SendAsync(AppSettings settings, int universe, byte[] dmxData, CancellationToken cancellationToken)
    {
        Frames.Add((universe, dmxData.ToArray()));
        return Task.CompletedTask;
    }
}
