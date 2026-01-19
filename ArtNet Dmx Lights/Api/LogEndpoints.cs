using ArtNet_Dmx_Lights.Models;
using ArtNet_Dmx_Lights.Services;

namespace ArtNet_Dmx_Lights.Api;

public static class LogEndpoints
{
    public static IEndpointRouteBuilder MapLogEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/logs");

        group.MapGet("/", async (string? from,
                string? to,
                string? type,
                Guid? presetId,
                Guid? groupId,
                IAppStateStore store,
                CancellationToken cancellationToken) =>
            {
                if (!TryParseRange(from, to, out var fromTime, out var toTime, out var error))
                {
                    return Results.BadRequest(new { errors = new[] { error } });
                }

                LogEventType? eventType = null;
                if (!string.IsNullOrWhiteSpace(type))
                {
                    if (!Enum.TryParse<LogEventType>(type, true, out var parsed))
                    {
                        return Results.BadRequest(new { errors = new[] { "Invalid log type." } });
                    }

                    eventType = parsed;
                }

                var snapshot = await store.GetSnapshotAsync(cancellationToken);
                var logs = snapshot.Logs.AsEnumerable();

                if (fromTime.HasValue)
                {
                    logs = logs.Where(l => l.TimestampUtc >= fromTime.Value);
                }

                if (toTime.HasValue)
                {
                    logs = logs.Where(l => l.TimestampUtc <= toTime.Value);
                }

                if (eventType.HasValue)
                {
                    logs = logs.Where(l => l.Type == eventType.Value);
                }

                if (presetId.HasValue)
                {
                    logs = logs.Where(l => l.PresetId == presetId);
                }

                if (groupId.HasValue)
                {
                    logs = logs.Where(l => l.GroupId == groupId);
                }

                return Results.Ok(logs.ToList());
            });

        return routes;
    }

    private static bool TryParseRange(string? from, string? to, out DateTimeOffset? fromTime, out DateTimeOffset? toTime, out string error)
    {
        fromTime = null;
        toTime = null;
        error = string.Empty;
        DateTimeOffset fromParsed = default;
        DateTimeOffset toParsed = default;

        if (!string.IsNullOrWhiteSpace(from) && !DateTimeOffset.TryParse(from, out fromParsed))
        {
            error = "Invalid from timestamp.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(to) && !DateTimeOffset.TryParse(to, out toParsed))
        {
            error = "Invalid to timestamp.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(from))
        {
            fromTime = fromParsed;
        }

        if (!string.IsNullOrWhiteSpace(to))
        {
            toTime = toParsed;
        }

        return true;
    }
}
