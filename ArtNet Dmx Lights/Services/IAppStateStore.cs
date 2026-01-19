using ArtNet_Dmx_Lights.Models;

namespace ArtNet_Dmx_Lights.Services;

public interface IAppStateStore
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<AppState> GetSnapshotAsync(CancellationToken cancellationToken);
    Task<T> UpdateAsync<T>(Func<AppState, T> action, CancellationToken cancellationToken);
}
