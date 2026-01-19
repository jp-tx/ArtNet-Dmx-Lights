using ArtNet_Dmx_Lights.Models;
using ArtNet_Dmx_Lights.Services;
using Microsoft.AspNetCore.Mvc;

namespace ArtNet_Dmx_Lights.Api;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/settings");

        group.MapGet("/", async (IAppStateStore store, CancellationToken cancellationToken) =>
        {
            var snapshot = await store.GetSnapshotAsync(cancellationToken);
            return Results.Ok(snapshot.Settings);
        });

        group.MapPut("/", async ([FromBody] AppSettings settings,
                IAppStateStore store,
                ValidationService validator,
                IGeoTimeLookupService geoLookup,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var validation = validator.ValidateSettings(settings);
                if (!validation.IsValid)
                {
                    return Results.BadRequest(new { errors = validation.Errors });
                }

                var snapshot = await store.GetSnapshotAsync(cancellationToken);
                var currentSettings = snapshot.Settings;
                string? resolutionWarning = null;

                var zipChanged = !string.Equals(settings.ZipCode, currentSettings.ZipCode, StringComparison.OrdinalIgnoreCase);
                var manualCoordsChanged = settings.ManualLat != currentSettings.ManualLat ||
                                          settings.ManualLon != currentSettings.ManualLon;
                var hasManualOverride = !string.IsNullOrWhiteSpace(settings.ManualTimeZone) ||
                                        (settings.ManualLat.HasValue && settings.ManualLon.HasValue);
                var resolvedSuccess = false;

                if (!string.IsNullOrWhiteSpace(settings.ZipCode))
                {
                    GeoTimeResolution? resolved = null;
                    try
                    {
                        resolved = await geoLookup.ResolveAsync(settings.ZipCode, cancellationToken);
                    }
                    catch
                    {
                        resolutionWarning = hasManualOverride
                            ? $"Could not resolve ZIP {settings.ZipCode}. Manual overrides will be used."
                            : $"Could not resolve ZIP {settings.ZipCode}. Keeping cached coordinates/time zone.";
                    }

                    if (resolved is not null)
                    {
                        settings.ResolvedLat = resolved.Value.Lat;
                        settings.ResolvedLon = resolved.Value.Lon;
                        settings.ResolvedTimeZone = resolved.Value.TimeZone;
                        resolvedSuccess = true;
                    }
                    else
                    {
                        settings.ResolvedLat = currentSettings.ResolvedLat;
                        settings.ResolvedLon = currentSettings.ResolvedLon;
                        settings.ResolvedTimeZone = currentSettings.ResolvedTimeZone;
                        resolutionWarning ??= hasManualOverride
                            ? $"Could not resolve ZIP {settings.ZipCode}. Manual overrides will be used."
                            : $"Could not resolve ZIP {settings.ZipCode}. Keeping cached coordinates/time zone.";
                    }
                }
                else
                {
                    settings.ResolvedLat = currentSettings.ResolvedLat;
                    settings.ResolvedLon = currentSettings.ResolvedLon;
                    settings.ResolvedTimeZone = currentSettings.ResolvedTimeZone;
                }

                if (!zipChanged || !resolvedSuccess)
                {
                    settings.LastSunriseUtc = currentSettings.LastSunriseUtc;
                    settings.LastSunsetUtc = currentSettings.LastSunsetUtc;
                }
                else
                {
                    settings.LastSunriseUtc = null;
                    settings.LastSunsetUtc = null;
                }

                if (manualCoordsChanged)
                {
                    settings.LastSunriseUtc = null;
                    settings.LastSunsetUtc = null;
                }

                await store.UpdateAsync(state =>
                {
                    var previousBase = state.Settings.UniverseBase;
                    if (settings.UniverseBase != previousBase)
                    {
                        var delta = settings.UniverseBase - previousBase;
                        foreach (var group in state.Groups)
                        {
                            group.Universe += delta;
                        }
                    }

                    state.Settings = settings;
                    return true;
                }, cancellationToken);

                if (!string.IsNullOrWhiteSpace(resolutionWarning))
                {
                    httpContext.Response.Headers["X-Geo-Resolve-Warning"] = resolutionWarning;
                }

                return Results.Ok(settings);
            });

        return routes;
    }
}
