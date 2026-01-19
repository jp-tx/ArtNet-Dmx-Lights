using System.Net.Sockets;
using ArtNet_Dmx_Lights.Models;

namespace ArtNet_Dmx_Lights.Services;

public sealed class ArtNetSender : IArtNetSender, IDisposable
{
    private readonly UdpClient _client = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task SendAsync(AppSettings settings, int universe, byte[] dmxData, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ControllerHost))
        {
            return;
        }

        var packet = ArtNetPacketBuilder.BuildDmxPacket(settings, universe, dmxData);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await _client.SendAsync(packet, packet.Length, settings.ControllerHost, settings.ArtnetPort);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        _lock.Dispose();
    }
}
