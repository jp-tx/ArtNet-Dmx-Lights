using ArtNet_Dmx_Lights.Models;

namespace ArtNet_Dmx_Lights.Services;

public sealed class StartupService : IHostedService
{
    private readonly IAppStateStore _store;
    private readonly DmxEngine _engine;

    public StartupService(IAppStateStore store, DmxEngine engine)
    {
        _store = store;
        _engine = engine;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var snapshot = await _store.GetSnapshotAsync(cancellationToken);
        if (snapshot.Runtime.LastScheduledPresetId.HasValue)
        {
            var applied = await _engine.ActivatePresetAsync(snapshot.Runtime.LastScheduledPresetId.Value, ActiveSource.Schedule, cancellationToken);
            if (applied)
            {
                return;
            }
        }

        await _engine.ZeroGroupsAsync(snapshot.Settings, snapshot.Groups, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
