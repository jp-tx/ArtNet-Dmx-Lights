using ArtNet_Dmx_Lights.Models;
using ArtNet_Dmx_Lights.Services;
using Microsoft.AspNetCore.Mvc;

namespace ArtNet_Dmx_Lights.Api;

public static class ScheduleEndpoints
{
    public static IEndpointRouteBuilder MapScheduleEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/schedules");

        group.MapGet("/", async (IAppStateStore store, CancellationToken cancellationToken) =>
        {
            var snapshot = await store.GetSnapshotAsync(cancellationToken);
            return Results.Ok(snapshot.Schedules);
        });

        group.MapGet("/{scheduleId:guid}", async (Guid scheduleId, IAppStateStore store, CancellationToken cancellationToken) =>
        {
            var snapshot = await store.GetSnapshotAsync(cancellationToken);
            var schedule = snapshot.Schedules.FirstOrDefault(s => s.Id == scheduleId);
            return schedule is null ? Results.NotFound() : Results.Ok(schedule);
        });

        group.MapPost("/", async ([FromBody] Schedule input,
                IAppStateStore store,
                ValidationService validator,
                CancellationToken cancellationToken) =>
            await store.UpdateAsync(state =>
            {
                var scheduleId = input.Id == Guid.Empty ? Guid.NewGuid() : input.Id;
                if (state.Schedules.Any(s => s.Id == scheduleId))
                {
                    return Results.BadRequest(new { errors = new[] { "Schedule ID already exists." } });
                }

                var scheduleToAdd = new Schedule
                {
                    Id = scheduleId,
                    Name = input.Name,
                    PresetId = input.PresetId,
                    Type = input.Type,
                    Time = input.Time,
                    OffsetMinutes = input.OffsetMinutes,
                    Enabled = input.Enabled,
                    UpdatedAt = DateTimeOffset.UtcNow
                };

                var validation = validator.ValidateSchedule(scheduleToAdd, state.Presets);
                if (!validation.IsValid)
                {
                    return Results.BadRequest(new { errors = validation.Errors });
                }

                state.Schedules.Add(scheduleToAdd);
                return Results.Ok(scheduleToAdd);
            }, cancellationToken));

        group.MapPut("/{scheduleId:guid}", async (Guid scheduleId,
                [FromBody] Schedule input,
                IAppStateStore store,
                ValidationService validator,
                CancellationToken cancellationToken) =>
            await store.UpdateAsync(state =>
            {
                var index = state.Schedules.FindIndex(s => s.Id == scheduleId);
                if (index < 0)
                {
                    return Results.NotFound();
                }

                var scheduleToUpdate = new Schedule
                {
                    Id = scheduleId,
                    Name = input.Name,
                    PresetId = input.PresetId,
                    Type = input.Type,
                    Time = input.Time,
                    OffsetMinutes = input.OffsetMinutes,
                    Enabled = input.Enabled,
                    UpdatedAt = DateTimeOffset.UtcNow
                };

                var validation = validator.ValidateSchedule(scheduleToUpdate, state.Presets);
                if (!validation.IsValid)
                {
                    return Results.BadRequest(new { errors = validation.Errors });
                }

                state.Schedules[index] = scheduleToUpdate;
                return Results.Ok(scheduleToUpdate);
            }, cancellationToken));

        group.MapDelete("/{scheduleId:guid}", async (Guid scheduleId, IAppStateStore store, CancellationToken cancellationToken) =>
            await store.UpdateAsync(state =>
            {
                var removed = state.Schedules.RemoveAll(s => s.Id == scheduleId);
                return removed == 0 ? Results.NotFound() : Results.NoContent();
            }, cancellationToken));

        return routes;
    }
}
