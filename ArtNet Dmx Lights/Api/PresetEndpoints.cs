using ArtNet_Dmx_Lights.Models;
using ArtNet_Dmx_Lights.Services;
using Microsoft.AspNetCore.Mvc;

namespace ArtNet_Dmx_Lights.Api;

public static class PresetEndpoints
{
    public static IEndpointRouteBuilder MapPresetEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/presets");

        group.MapGet("/", async (IAppStateStore store, CancellationToken cancellationToken) =>
        {
            var snapshot = await store.GetSnapshotAsync(cancellationToken);
            return Results.Ok(snapshot.Presets);
        });

        group.MapGet("/{presetId:guid}", async (Guid presetId, IAppStateStore store, CancellationToken cancellationToken) =>
        {
            var snapshot = await store.GetSnapshotAsync(cancellationToken);
            var preset = snapshot.Presets.FirstOrDefault(p => p.Id == presetId);
            return preset is null ? Results.NotFound() : Results.Ok(preset);
        });

        group.MapPost("/", async ([FromBody] Preset input,
                IAppStateStore store,
                ValidationService validator,
                CancellationToken cancellationToken) =>
            await store.UpdateAsync(state =>
            {
                var presetId = input.Id == Guid.Empty ? Guid.NewGuid() : input.Id;
                if (state.Presets.Any(p => p.Id == presetId))
                {
                    return Results.BadRequest(new { errors = new[] { "Preset ID already exists." } });
                }

                var presetToAdd = new Preset
                {
                    Id = presetId,
                    Name = input.Name,
                    FadeMs = input.FadeMs,
                    Groups = input.Groups.Select(pg => new PresetGroup
                    {
                        GroupId = pg.GroupId,
                        Values = pg.Values?.ToArray() ?? []
                    }).ToList()
                };

                var validation = validator.ValidatePreset(presetToAdd, state.Groups);
                if (!validation.IsValid)
                {
                    return Results.BadRequest(new { errors = validation.Errors });
                }

                state.Presets.Add(presetToAdd);
                state.Logs.Add(new LogEntry
                {
                    Id = Guid.NewGuid(),
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Type = LogEventType.PresetSaved,
                    PresetId = presetToAdd.Id,
                    PresetName = presetToAdd.Name
                });

                return Results.Ok(presetToAdd);
            }, cancellationToken));

        group.MapPut("/{presetId:guid}", async (Guid presetId,
                [FromBody] Preset input,
                IAppStateStore store,
                ValidationService validator,
                CancellationToken cancellationToken) =>
            await store.UpdateAsync(state =>
            {
                var index = state.Presets.FindIndex(p => p.Id == presetId);
                if (index < 0)
                {
                    return Results.NotFound();
                }

                var presetToUpdate = new Preset
                {
                    Id = presetId,
                    Name = input.Name,
                    FadeMs = input.FadeMs,
                    Groups = input.Groups.Select(pg => new PresetGroup
                    {
                        GroupId = pg.GroupId,
                        Values = pg.Values?.ToArray() ?? []
                    }).ToList()
                };

                var validation = validator.ValidatePreset(presetToUpdate, state.Groups);
                if (!validation.IsValid)
                {
                    return Results.BadRequest(new { errors = validation.Errors });
                }

                state.Presets[index] = presetToUpdate;
                state.Logs.Add(new LogEntry
                {
                    Id = Guid.NewGuid(),
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Type = LogEventType.PresetUpdated,
                    PresetId = presetToUpdate.Id,
                    PresetName = presetToUpdate.Name
                });

                return Results.Ok(presetToUpdate);
            }, cancellationToken));

        group.MapDelete("/{presetId:guid}", async (Guid presetId, IAppStateStore store, CancellationToken cancellationToken) =>
            await store.UpdateAsync(state =>
            {
                var preset = state.Presets.FirstOrDefault(p => p.Id == presetId);
                if (preset is null)
                {
                    return Results.NotFound();
                }

                state.Presets.RemoveAll(p => p.Id == presetId);
                state.Schedules.RemoveAll(s => s.PresetId == presetId);
                state.Logs.Add(new LogEntry
                {
                    Id = Guid.NewGuid(),
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Type = LogEventType.PresetDeleted,
                    PresetId = presetId,
                    PresetName = preset.Name
                });

                return Results.NoContent();
            }, cancellationToken));

        group.MapPost("/{presetId:guid}/activate", async (Guid presetId,
                DmxEngine engine,
                CancellationToken cancellationToken) =>
            {
                var activated = await engine.ActivatePresetAsync(presetId, ActiveSource.Manual, cancellationToken);
                return activated ? Results.NoContent() : Results.NotFound();
            });

        group.MapPost("/preview", async ([FromBody] Preset input,
                IAppStateStore store,
                ValidationService validator,
                DmxEngine engine,
                CancellationToken cancellationToken) =>
            {
                var snapshot = await store.GetSnapshotAsync(cancellationToken);
                var previewPreset = new Preset
                {
                    Id = input.Id,
                    Name = string.IsNullOrWhiteSpace(input.Name) ? "Preview" : input.Name,
                    FadeMs = input.FadeMs,
                    Groups = input.Groups.Select(pg => new PresetGroup
                    {
                        GroupId = pg.GroupId,
                        Values = pg.Values?.ToArray() ?? []
                    }).ToList()
                };

                var validation = validator.ValidatePreset(previewPreset, snapshot.Groups);
                if (!validation.IsValid)
                {
                    return Results.BadRequest(new { errors = validation.Errors });
                }

                await engine.PreviewPresetAsync(previewPreset, cancellationToken);
                return Results.NoContent();
            });

        return routes;
    }
}
