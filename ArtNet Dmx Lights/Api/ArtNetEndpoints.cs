using ArtNet_Dmx_Lights.Services;

namespace ArtNet_Dmx_Lights.Api;

public static class ArtNetEndpoints
{
    public static IEndpointRouteBuilder MapArtNetEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/artnet");

        group.MapGet("/discover", async (int? timeoutMs,
                IAppStateStore store,
                ArtNetDiscoveryService discovery,
                CancellationToken cancellationToken) =>
            {
                var snapshot = await store.GetSnapshotAsync(cancellationToken);
                var timeout = timeoutMs.HasValue ? Math.Clamp(timeoutMs.Value, 200, 10000) : 1500;
                var response = await discovery.DiscoverAsync(snapshot.Settings, timeout, cancellationToken);
                return Results.Ok(response);
            });

        return routes;
    }
}
