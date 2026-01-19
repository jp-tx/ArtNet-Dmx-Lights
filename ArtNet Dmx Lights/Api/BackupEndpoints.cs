using ArtNet_Dmx_Lights.Models;
using ArtNet_Dmx_Lights.Services;
using Microsoft.AspNetCore.Mvc;

namespace ArtNet_Dmx_Lights.Api;

public static class BackupEndpoints
{
    public static IEndpointRouteBuilder MapBackupEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/backup");

        group.MapGet("/", async (IAppStateStore store, CancellationToken cancellationToken) =>
        {
            var snapshot = await store.GetSnapshotAsync(cancellationToken);
            var bundle = new BackupBundle
            {
                Settings = snapshot.Settings,
                Groups = snapshot.Groups,
                Presets = snapshot.Presets,
                Schedules = snapshot.Schedules
            };

            return Results.Ok(bundle);
        });

        group.MapPost("/", async ([FromBody] BackupBundle bundle, IAppStateStore store, CancellationToken cancellationToken) =>
            await store.UpdateAsync(state =>
            {
                state.Settings = bundle.Settings ?? new AppSettings();
                state.Groups = bundle.Groups ?? [];
                state.Presets = bundle.Presets ?? [];
                state.Schedules = bundle.Schedules ?? [];
                state.Runtime = new RuntimeState();
                return Results.NoContent();
            }, cancellationToken));

        return routes;
    }
}
