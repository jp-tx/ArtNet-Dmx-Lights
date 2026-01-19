using System.Text.Json;
using ArtNet_Dmx_Lights.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace ArtNetDmxLights.Tests;

public class TestAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dataPath = Path.Combine(Path.GetTempPath(), "ArtNetDmxLightsTests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAppStateStore>();
            services.AddSingleton<IAppStateStore>(sp =>
                new AppStateStore(sp.GetRequiredService<JsonSerializerOptions>(), _dataPath));

            services.RemoveAll<IGeoTimeLookupService>();
            services.AddSingleton<IGeoTimeLookupService>(new TestGeoTimeLookupService());

            services.RemoveAll<IArtNetSender>();
            services.AddSingleton<IArtNetSender, NullArtNetSender>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (Directory.Exists(_dataPath))
        {
            Directory.Delete(_dataPath, true);
        }
    }
}

public sealed class TestGeoTimeLookupService : IGeoTimeLookupService
{
    public GeoTimeResolution? NextResolution { get; set; }

    public Task<GeoTimeResolution?> ResolveAsync(string zipCode, CancellationToken cancellationToken)
    {
        return Task.FromResult(NextResolution);
    }
}
