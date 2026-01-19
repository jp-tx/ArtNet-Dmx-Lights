using System.Globalization;
using System.Text.Json;

namespace ArtNet_Dmx_Lights.Services;

public sealed class SunriseSunsetService
{
    private readonly HttpClient _httpClient;

    public SunriseSunsetService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<SunTimes?> GetAsync(double lat, double lon, DateOnly date, CancellationToken cancellationToken)
    {
        var url =
            $"https://api.sunrise-sunset.org/json?lat={lat.ToString(CultureInfo.InvariantCulture)}&lng={lon.ToString(CultureInfo.InvariantCulture)}&date={date:yyyy-MM-dd}&formatted=0";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("status", out var status) ||
            !string.Equals(status.GetString(), "OK", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!document.RootElement.TryGetProperty("results", out var results))
        {
            return null;
        }

        if (!results.TryGetProperty("sunrise", out var sunriseElement) ||
            !results.TryGetProperty("sunset", out var sunsetElement))
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(sunriseElement.GetString(), out var sunrise))
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(sunsetElement.GetString(), out var sunset))
        {
            return null;
        }

        return new SunTimes(sunrise, sunset);
    }
}

public readonly record struct SunTimes(DateTimeOffset SunriseUtc, DateTimeOffset SunsetUtc);
