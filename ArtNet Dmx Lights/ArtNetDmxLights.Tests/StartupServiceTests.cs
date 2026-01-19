using System.Text.Json;
using System.Linq;
using ArtNet_Dmx_Lights.Models;
using ArtNet_Dmx_Lights.Services;
using Xunit;

namespace ArtNetDmxLights.Tests;

public sealed class StartupServiceTests
{
    [Fact]
    public async Task Startup_AppliesLastScheduledPreset()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "ArtNetDmxLightsTests", Guid.NewGuid().ToString("N"));
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var store = new AppStateStore(jsonOptions, tempPath);
        await store.InitializeAsync(CancellationToken.None);

        var groupId = Guid.NewGuid();
        var presetId = Guid.NewGuid();

        await store.UpdateAsync(state =>
        {
            state.Settings.UniverseBase = 0;
            state.Groups.Add(new FixtureGroup
            {
                Id = groupId,
                Name = "Group",
                Universe = 0,
                StartChannel = 1,
                ChannelCount = 1
            });
            state.Presets.Add(new Preset
            {
                Id = presetId,
                Name = "Preset",
                FadeMs = 0,
                Groups =
                [
                    new PresetGroup
                    {
                        GroupId = groupId,
                        Values = [100]
                    }
                ]
            });
            state.Runtime.LastScheduledPresetId = presetId;
            return true;
        }, CancellationToken.None);

        var sender = new TestArtNetSender();
        var cache = new DmxStateCache();
        var engine = new DmxEngine(store, sender, cache);
        var startup = new StartupService(store, engine);

        await startup.StartAsync(CancellationToken.None);

        var snapshot = await store.GetSnapshotAsync(CancellationToken.None);
        Assert.Equal(presetId, snapshot.Runtime.ActivePresetId);
    }

    [Fact]
    public async Task Startup_ZerosGroups_WhenNoScheduledPreset()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "ArtNetDmxLightsTests", Guid.NewGuid().ToString("N"));
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var store = new AppStateStore(jsonOptions, tempPath);
        await store.InitializeAsync(CancellationToken.None);

        var groupId = Guid.NewGuid();
        await store.UpdateAsync(state =>
        {
            state.Settings.UniverseBase = 0;
            state.Groups.Add(new FixtureGroup
            {
                Id = groupId,
                Name = "Group",
                Universe = 0,
                StartChannel = 1,
                ChannelCount = 1
            });
            return true;
        }, CancellationToken.None);

        var sender = new TestArtNetSender();
        var cache = new DmxStateCache();
        cache.SetState(new[]
        {
            new[] { 50 }.Concat(new int[511]).ToArray(),
            new int[512]
        });

        var engine = new DmxEngine(store, sender, cache);
        var startup = new StartupService(store, engine);

        await startup.StartAsync(CancellationToken.None);

        var snapshot = cache.GetSnapshot();
        Assert.Equal(0, snapshot[0][0]);
    }
}
