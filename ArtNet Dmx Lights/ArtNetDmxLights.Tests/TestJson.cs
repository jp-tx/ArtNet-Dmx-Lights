using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArtNetDmxLights.Tests;

public static class TestJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
