using ArtNet_Dmx_Lights.Models;
using ArtNet_Dmx_Lights.Services;
using Xunit;

namespace ArtNetDmxLights.Tests;

public sealed class ArtNetPacketBuilderTests
{
    [Fact]
    public void BuildDmxPacket_SetsHeaderAndAddressing()
    {
        var settings = new AppSettings
        {
            UniverseBase = 0,
            ArtnetNet = 1,
            ArtnetSubNet = 2
        };

        var data = new byte[512];
        data[0] = 10;
        data[1] = 20;

        var packet = ArtNetPacketBuilder.BuildDmxPacket(settings, universe: 1, dmxData: data);

        Assert.Equal(18 + 512, packet.Length);
        Assert.Equal((byte)'A', packet[0]);
        Assert.Equal((byte)'r', packet[1]);
        Assert.Equal((byte)'t', packet[2]);
        Assert.Equal((byte)0x00, packet[7]);

        Assert.Equal(0x00, packet[8]);
        Assert.Equal(0x50, packet[9]);

        Assert.Equal(0x00, packet[10]);
        Assert.Equal(0x0E, packet[11]);

        var expectedSubUni = (byte)((settings.ArtnetSubNet << 4) | 0x01);
        Assert.Equal(expectedSubUni, packet[14]);
        Assert.Equal((byte)settings.ArtnetNet, packet[15]);

        Assert.Equal(0x02, packet[16]);
        Assert.Equal(0x00, packet[17]);

        Assert.Equal(10, packet[18]);
        Assert.Equal(20, packet[19]);
    }
}
