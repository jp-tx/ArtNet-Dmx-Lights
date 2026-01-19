using System.Text;
using ArtNet_Dmx_Lights.Models;

namespace ArtNet_Dmx_Lights.Services;

public static class ArtNetPacketBuilder
{
    private static readonly byte[] ArtNetId = Encoding.ASCII.GetBytes("Art-Net\0");
    private const ushort OpCodeArtDmx = 0x5000;
    private const ushort ProtocolVersion = 14;

    public static byte[] BuildDmxPacket(AppSettings settings, int universe, byte[] dmxData)
    {
        var dataLength = Math.Clamp(dmxData.Length, 0, 512);
        var packet = new byte[18 + dataLength];

        Array.Copy(ArtNetId, packet, ArtNetId.Length);

        packet[8] = (byte)(OpCodeArtDmx & 0xFF);
        packet[9] = (byte)((OpCodeArtDmx >> 8) & 0xFF);

        packet[10] = (byte)((ProtocolVersion >> 8) & 0xFF);
        packet[11] = (byte)(ProtocolVersion & 0xFF);

        packet[12] = 0x00; // sequence
        packet[13] = 0x00; // physical

        var universeIndex = universe - settings.UniverseBase;
        if (universeIndex < 0)
        {
            universeIndex = 0;
        }

        var subUni = (byte)(universeIndex & 0x0F);
        packet[14] = (byte)((settings.ArtnetSubNet << 4) | subUni);
        packet[15] = (byte)(settings.ArtnetNet & 0x7F);

        packet[16] = (byte)((dataLength >> 8) & 0xFF);
        packet[17] = (byte)(dataLength & 0xFF);

        Array.Copy(dmxData, 0, packet, 18, dataLength);

        return packet;
    }
}
