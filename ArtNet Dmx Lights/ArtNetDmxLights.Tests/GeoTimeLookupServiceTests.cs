using System.Net;
using System.Net.Http;
using System.Text;
using ArtNet_Dmx_Lights.Services;
using Xunit;

namespace ArtNetDmxLights.Tests;

public sealed class GeoTimeLookupServiceTests
{
    [Fact]
    public async Task ResolveAsync_ReturnsResolution()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            var url = request.RequestUri?.AbsoluteUri ?? string.Empty;
            if (url == "https://api.zippopotam.us/us/37040")
            {
                const string payload = "{\"places\":[{\"latitude\":\"36.522\",\"longitude\":\"-87.349\"}]}";
                return JsonResponse(payload);
            }

            if (url == "https://api.open-meteo.com/v1/forecast?latitude=36.522&longitude=-87.349&current=temperature_2m&timezone=auto")
            {
                const string payload = "{\"timezone\":\"America/Chicago\"}";
                return JsonResponse(payload);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = new HttpClient(handler);
        var service = new GeoTimeLookupService(client);

        var result = await service.ResolveAsync("37040", CancellationToken.None);

        Assert.NotNull(result);
        Assert.InRange(result!.Value.Lat, 36.521, 36.523);
        Assert.InRange(result.Value.Lon, -87.350, -87.348);
        Assert.Equal("America/Chicago", result.Value.TimeZone);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNullWhenTimeZoneMissing()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            var url = request.RequestUri?.AbsoluteUri ?? string.Empty;
            if (url == "https://api.zippopotam.us/us/37040")
            {
                const string payload = "{\"places\":[{\"latitude\":\"36.522\",\"longitude\":\"-87.349\"}]}";
                return JsonResponse(payload);
            }

            if (url.StartsWith("https://api.open-meteo.com/v1/forecast?", StringComparison.Ordinal))
            {
                const string payload = "{\"utc_offset_seconds\":-21600}";
                return JsonResponse(payload);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = new HttpClient(handler);
        var service = new GeoTimeLookupService(client);

        var result = await service.ResolveAsync("37040", CancellationToken.None);

        Assert.Null(result);
    }

    private static HttpResponseMessage JsonResponse(string payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}
