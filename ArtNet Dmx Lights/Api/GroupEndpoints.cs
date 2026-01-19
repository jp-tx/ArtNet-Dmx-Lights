using ArtNet_Dmx_Lights.Models;
using ArtNet_Dmx_Lights.Services;
using Microsoft.AspNetCore.Mvc;

namespace ArtNet_Dmx_Lights.Api;

public static class GroupEndpoints
{
    public static IEndpointRouteBuilder MapGroupEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/groups");

        group.MapGet("/", async (IAppStateStore store, CancellationToken cancellationToken) =>
        {
            var snapshot = await store.GetSnapshotAsync(cancellationToken);
            return Results.Ok(snapshot.Groups);
        });

        group.MapGet("/{groupId:guid}", async (Guid groupId, IAppStateStore store, CancellationToken cancellationToken) =>
        {
            var snapshot = await store.GetSnapshotAsync(cancellationToken);
            var existing = snapshot.Groups.FirstOrDefault(g => g.Id == groupId);
            return existing is null ? Results.NotFound() : Results.Ok(existing);
        });

        group.MapPost("/", async ([FromBody] FixtureGroup input,
                IAppStateStore store,
                ValidationService validator,
                CancellationToken cancellationToken) =>
            await store.UpdateAsync(state =>
            {
                var groupId = input.Id == Guid.Empty ? Guid.NewGuid() : input.Id;
                if (state.Groups.Any(g => g.Id == groupId))
                {
                    return Results.BadRequest(new { errors = new[] { "Group ID already exists." } });
                }

                var groupToAdd = new FixtureGroup
                {
                    Id = groupId,
                    Name = input.Name,
                    Universe = input.Universe,
                    StartChannel = input.StartChannel,
                    ChannelCount = input.ChannelCount
                };

                var validation = validator.ValidateGroup(groupToAdd, state.Settings);
                if (!validation.IsValid)
                {
                    return Results.BadRequest(new { errors = validation.Errors });
                }

                state.Groups.Add(groupToAdd);
                return Results.Ok(groupToAdd);
            }, cancellationToken));

        group.MapPut("/{groupId:guid}", async (Guid groupId,
                [FromBody] FixtureGroup input,
                IAppStateStore store,
                ValidationService validator,
                CancellationToken cancellationToken) =>
            await store.UpdateAsync(state =>
            {
                var index = state.Groups.FindIndex(g => g.Id == groupId);
                if (index < 0)
                {
                    return Results.NotFound();
                }

                var updatedGroup = new FixtureGroup
                {
                    Id = groupId,
                    Name = input.Name,
                    Universe = input.Universe,
                    StartChannel = input.StartChannel,
                    ChannelCount = input.ChannelCount
                };

                var validation = validator.ValidateGroup(updatedGroup, state.Settings);
                if (!validation.IsValid)
                {
                    return Results.BadRequest(new { errors = validation.Errors });
                }

                var previousCount = state.Groups[index].ChannelCount;
                state.Groups[index] = updatedGroup;
                AdjustPresetValues(state.Presets, groupId, previousCount, updatedGroup.ChannelCount);

                return Results.Ok(updatedGroup);
            }, cancellationToken));

        group.MapDelete("/{groupId:guid}", async (Guid groupId, IAppStateStore store, CancellationToken cancellationToken) =>
            await store.UpdateAsync(state =>
            {
                var removed = state.Groups.RemoveAll(g => g.Id == groupId);
                if (removed == 0)
                {
                    return Results.NotFound();
                }

                var presetsToRemove = new HashSet<Guid>();
                foreach (var preset in state.Presets)
                {
                    preset.Groups.RemoveAll(pg => pg.GroupId == groupId);
                    if (preset.Groups.Count == 0)
                    {
                        presetsToRemove.Add(preset.Id);
                    }
                }

                if (presetsToRemove.Count > 0)
                {
                    state.Presets.RemoveAll(p => presetsToRemove.Contains(p.Id));
                    state.Schedules.RemoveAll(s => presetsToRemove.Contains(s.PresetId));
                }

                return Results.NoContent();
            }, cancellationToken));

        return routes;
    }

    private static void AdjustPresetValues(List<Preset> presets, Guid groupId, int previousCount, int newCount)
    {
        foreach (var preset in presets)
        {
            var entry = preset.Groups.FirstOrDefault(pg => pg.GroupId == groupId);
            if (entry is null)
            {
                continue;
            }

            if (newCount == previousCount)
            {
                continue;
            }

            if (newCount < entry.Values.Length)
            {
                entry.Values = entry.Values.Take(newCount).ToArray();
                continue;
            }

            if (newCount > entry.Values.Length)
            {
                var extended = new int[newCount];
                Array.Copy(entry.Values, extended, entry.Values.Length);
                entry.Values = extended;
            }
        }
    }
}
