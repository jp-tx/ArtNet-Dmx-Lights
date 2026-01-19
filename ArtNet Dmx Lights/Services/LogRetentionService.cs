using ArtNet_Dmx_Lights.Models;

namespace ArtNet_Dmx_Lights.Services;

public sealed class LogRetentionService : BackgroundService
{
    private readonly IAppStateStore _store;
    private readonly LogRetentionPolicy _policy;
    private readonly TimeSpan _retention = TimeSpan.FromHours(72);

    public LogRetentionService(IAppStateStore store, LogRetentionPolicy policy)
    {
        _store = store;
        _policy = policy;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await TrimAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private Task TrimAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.Subtract(_retention);
        return _store.UpdateAsync(state =>
        {
            _policy.Trim(state.Logs, cutoff);
            return true;
        }, cancellationToken);
    }
}
