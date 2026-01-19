using System.Net;
using System.Net.Http.Json;
using ArtNet_Dmx_Lights.Models;
using ArtNet_Dmx_Lights.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArtNetDmxLights.Tests;

public sealed class ApiSettingsTests
{
    [Fact]
    public async Task GetSettings_ReturnsCurrentValues()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/settings");
        response.EnsureSuccessStatusCode();

        var settings = await response.Content.ReadFromJsonAsync<AppSettings>(TestJson.Options);
        Assert.NotNull(settings);
    }

    [Fact]
    public async Task PutSettings_ValidatesInput()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/v1/settings", new AppSettings
        {
            ControllerHost = "",
            ArtnetPort = 6454,
            ArtnetNet = 0,
            ArtnetSubNet = 0,
            UniverseBase = 0,
            ZipCode = "78701"
        }, TestJson.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutSettings_FallsBackToCachedResolution()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();
        var geoService = factory.Services.GetRequiredService<IGeoTimeLookupService>() as TestGeoTimeLookupService;
        Assert.NotNull(geoService);

        geoService!.NextResolution = new GeoTimeResolution(30.27, -97.74, "America/Chicago");

        var initialResponse = await client.PutAsJsonAsync("/api/v1/settings", new AppSettings
        {
            ControllerHost = "controller.local",
            ArtnetPort = 6454,
            ArtnetNet = 0,
            ArtnetSubNet = 0,
            UniverseBase = 0,
            ZipCode = "78701"
        }, TestJson.Options);

        initialResponse.EnsureSuccessStatusCode();

        geoService.NextResolution = null;

        var secondResponse = await client.PutAsJsonAsync("/api/v1/settings", new AppSettings
        {
            ControllerHost = "controller.local",
            ArtnetPort = 6454,
            ArtnetNet = 0,
            ArtnetSubNet = 0,
            UniverseBase = 0,
            ZipCode = "78701"
        }, TestJson.Options);

        secondResponse.EnsureSuccessStatusCode();
        var settings = await secondResponse.Content.ReadFromJsonAsync<AppSettings>(TestJson.Options);

        Assert.NotNull(settings);
        Assert.Equal(30.27, settings!.ResolvedLat);
        Assert.Equal(-97.74, settings.ResolvedLon);
        Assert.Equal("America/Chicago", settings.ResolvedTimeZone);
    }

    [Fact]
    public async Task PutSettings_PersistsManualOverrides()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/v1/settings", new AppSettings
        {
            ControllerHost = "controller.local",
            ArtnetPort = 6454,
            ArtnetNet = 0,
            ArtnetSubNet = 0,
            UniverseBase = 0,
            ZipCode = "78701",
            ManualTimeZone = "America/Chicago",
            ManualLat = 36.522,
            ManualLon = -87.349
        }, TestJson.Options);

        response.EnsureSuccessStatusCode();
        var settings = await response.Content.ReadFromJsonAsync<AppSettings>(TestJson.Options);

        Assert.NotNull(settings);
        Assert.Equal("America/Chicago", settings!.ManualTimeZone);
        Assert.Equal(36.522, settings.ManualLat);
        Assert.Equal(-87.349, settings.ManualLon);
    }
}
