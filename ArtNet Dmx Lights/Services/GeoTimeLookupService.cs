using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace ArtNet_Dmx_Lights.Services;

public interface IGeoTimeLookupService
{
    Task<GeoTimeResolution?> ResolveAsync(string zipCode, CancellationToken cancellationToken);
}

public sealed class GeoTimeLookupService : IGeoTimeLookupService
{
    private readonly HttpClient _httpClient;

    public GeoTimeLookupService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<GeoTimeResolution?> ResolveAsync(string zipCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(zipCode))
        {
            return null;
        }

        var geo = await GetLatLonAsync(zipCode, cancellationToken);
        if (geo is null)
        {
            return null;
        }

        var timeZone = await GetTimeZoneAsync(geo.Value.Lat, geo.Value.Lon, cancellationToken);
        if (string.IsNullOrWhiteSpace(timeZone))
        {
            return null;
        }

        return new GeoTimeResolution(geo.Value.Lat, geo.Value.Lon, timeZone);
    }

    private async Task<(double Lat, double Lon)?> GetLatLonAsync(string zipCode, CancellationToken cancellationToken)
    {
        var url = $"https://api.zippopotam.us/us/{zipCode}";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("places", out var places) || places.GetArrayLength() == 0)
        {
            return null;
        }

        var place = places[0];
        if (!place.TryGetProperty("latitude", out var latElement) ||
            !place.TryGetProperty("longitude", out var lonElement))
        {
            return null;
        }

        if (!double.TryParse(latElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
        {
            return null;
        }

        if (!double.TryParse(lonElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
        {
            return null;
        }

        return (lat, lon);
    }

    private async Task<string?> GetTimeZoneAsync(double lat, double lon, CancellationToken cancellationToken)
    {
        var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat.ToString(CultureInfo.InvariantCulture)}&longitude={lon.ToString(CultureInfo.InvariantCulture)}&current=temperature_2m&timezone=auto";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (payload.TryGetProperty("timezone", out var tzElement))
        {
            return tzElement.GetString();
        }

        return null;
    }
}

public readonly record struct GeoTimeResolution(double Lat, double Lon, string TimeZone);
