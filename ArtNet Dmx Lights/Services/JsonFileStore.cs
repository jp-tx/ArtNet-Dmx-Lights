using System.Text.Json;

namespace ArtNet_Dmx_Lights.Services;

public sealed class JsonFileStore
{
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonFileStore(JsonSerializerOptions jsonOptions)
    {
        _jsonOptions = jsonOptions;
    }

    public async Task<T> ReadAsync<T>(string path, T defaultValue, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return defaultValue;
        }

        await using var stream = File.OpenRead(path);
        var data = await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, cancellationToken);
        return data ?? defaultValue;
    }

    public async Task WriteAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, value, _jsonOptions, cancellationToken);
        }

        File.Copy(tempPath, path, true);
        File.Delete(tempPath);
    }
}
