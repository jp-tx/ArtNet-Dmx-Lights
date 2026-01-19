using ArtNet_Dmx_Lights.Models;

namespace ArtNet_Dmx_Lights.Services;

public interface IArtNetSender
{
    Task SendAsync(AppSettings settings, int universe, byte[] dmxData, CancellationToken cancellationToken);
}
