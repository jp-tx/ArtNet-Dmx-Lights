using ArtNet_Dmx_Lights.Services;

namespace ArtNet_Dmx_Lights.Api;

public static class StatusEndpoints
{
    public static IEndpointRouteBuilder MapStatusEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/status");

        group.MapGet("/", async (IAppStateStore store, CancellationToken cancellationToken) =>
        {
            var snapshot = await store.GetSnapshotAsync(cancellationToken);
            return Results.Ok(new
            {
                activePresetId = snapshot.Runtime.ActivePresetId,
                activeSource = snapshot.Runtime.ActiveSource?.ToString().ToLowerInvariant(),
                lastScheduledAt = snapshot.Runtime.LastScheduledAt
            });
        });

        return routes;
    }
}
