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
            var ordered = snapshot.Presets
                .OrderBy(p => p.ListOrder)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase);
            return Results.Ok(ordered);
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

                var nextOrder = state.Presets.Count == 0 ? 1 : state.Presets.Max(p => p.ListOrder) + 1;
                var nextGrid = state.Presets.Count == 0 ? 0 : state.Presets.Max(p => p.GridLocation) + 1;
                var presetToAdd = new Preset
                {
                    Id = presetId,
                    Name = input.Name,
                    ListOrder = nextOrder,
                    GridLocation = nextGrid,
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
                    ListOrder = state.Presets[index].ListOrder,
                    GridLocation = state.Presets[index].GridLocation,
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

        group.MapPost("/{presetId:guid}/move", async (Guid presetId,
                string direction,
                IAppStateStore store,
                CancellationToken cancellationToken) =>
            await store.UpdateAsync(state =>
            {
                var ordered = state.Presets
                    .OrderBy(p => p.ListOrder)
                    .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var index = ordered.FindIndex(p => p.Id == presetId);
                if (index < 0)
                {
                    return Results.NotFound();
                }

                var normalized = direction?.Trim().ToLowerInvariant();
                if (normalized != "up" && normalized != "down")
                {
                    return Results.BadRequest(new { errors = new[] { "Direction must be 'up' or 'down'." } });
                }

                var targetIndex = normalized == "up" ? index - 1 : index + 1;
                if (targetIndex < 0 || targetIndex >= ordered.Count)
                {
                    return Results.NoContent();
                }

                var current = ordered[index];
                var neighbor = ordered[targetIndex];
                (current.ListOrder, neighbor.ListOrder) = (neighbor.ListOrder, current.ListOrder);

                return Results.NoContent();
            }, cancellationToken));

        group.MapPost("/{presetId:guid}/grid", async (Guid presetId,
                int targetIndex,
                IAppStateStore store,
                CancellationToken cancellationToken) =>
            await store.UpdateAsync(state =>
            {
                var preset = state.Presets.FirstOrDefault(p => p.Id == presetId);
                if (preset is null)
                {
                    return Results.NotFound();
                }

                if (state.Presets.Count <= 1)
                {
                    return Results.NoContent();
                }

                targetIndex = Math.Max(0, targetIndex);
                var maxLocation = state.Presets.Max(p => p.GridLocation);
                var slotCount = Math.Max(maxLocation, targetIndex) + 1;
                if (slotCount < 1)
                {
                    return Results.NoContent();
                }

                var slots = new Preset?[slotCount];
                foreach (var item in state.Presets)
                {
                    var index = item.GridLocation;
                    if (index < 0)
                    {
                        index = 0;
                    }

                    if (index >= slotCount)
                    {
                        index = slotCount - 1;
                    }

                    slots[index] = item;
                }

                var fromIndex = preset.GridLocation;
                if (fromIndex < 0 || fromIndex >= slotCount)
                {
                    fromIndex = Array.IndexOf(slots, preset);
                    if (fromIndex < 0)
                    {
                        return Results.NoContent();
                    }
                }

                if (fromIndex == targetIndex)
                {
                    return Results.NoContent();
                }

                if (slots[targetIndex] is null)
                {
                    slots[fromIndex] = null;
                    slots[targetIndex] = preset;
                }
                else if (targetIndex < fromIndex)
                {
                    for (var i = fromIndex; i > targetIndex; i--)
                    {
                        slots[i] = slots[i - 1];
                    }

                    slots[targetIndex] = preset;
                }
                else
                {
                    for (var i = fromIndex; i < targetIndex; i++)
                    {
                        slots[i] = slots[i + 1];
                    }

                    slots[targetIndex] = preset;
                }

                for (var i = 0; i < slots.Length; i++)
                {
                    if (slots[i] is not null)
                    {
                        slots[i]!.GridLocation = i;
                    }
                }

                return Results.NoContent();
            }, cancellationToken));

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
                    ListOrder = input.ListOrder,
                    GridLocation = input.GridLocation,
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

        group.MapPost("/fix-order", async (IAppStateStore store, CancellationToken cancellationToken) =>
            await store.UpdateAsync(state =>
            {
                var listOrdered = state.Presets
                    .OrderBy(p => p.ListOrder)
                    .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                for (var i = 0; i < listOrdered.Count; i++)
                {
                    listOrdered[i].ListOrder = i + 1;
                }

                var gridOrdered = state.Presets
                    .OrderBy(p => p.GridLocation)
                    .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                for (var i = 0; i < gridOrdered.Count; i++)
                {
                    gridOrdered[i].GridLocation = i;
                }

                return Results.NoContent();
            }, cancellationToken));

        return routes;
    }
}
