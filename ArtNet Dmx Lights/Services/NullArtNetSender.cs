using ArtNet_Dmx_Lights.Models;

namespace ArtNet_Dmx_Lights.Services;

public sealed class NullArtNetSender : IArtNetSender
{
    public Task SendAsync(AppSettings settings, int universe, byte[] dmxData, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
