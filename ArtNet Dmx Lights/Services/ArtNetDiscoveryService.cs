using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using ArtNet_Dmx_Lights.Models;

namespace ArtNet_Dmx_Lights.Services;

public sealed class ArtNetDiscoveryService
{
    private static readonly byte[] ArtNetId = Encoding.ASCII.GetBytes("Art-Net\0");
    private const ushort OpCodeArtPoll = 0x2000;
    private const ushort OpCodeArtPollReply = 0x2100;
    private const ushort ProtocolVersion = 14;

    public async Task<ArtNetDiscoveryResponse> DiscoverAsync(AppSettings settings, int timeoutMs, CancellationToken cancellationToken)
    {
        var response = new ArtNetDiscoveryResponse { TimeoutMs = timeoutMs };
        var targets = GetBroadcastTargets();
        if (targets.Count == 0)
        {
            response.Warning = "No broadcast targets available.";
            return response;
        }

        using var client = new UdpClient(AddressFamily.InterNetwork);
        client.EnableBroadcast = true;

        try
        {
            client.Client.Bind(new IPEndPoint(IPAddress.Any, settings.ArtnetPort));
        }
        catch (SocketException)
        {
            response.Warning = $"Could not bind UDP port {settings.ArtnetPort}. Using an ephemeral port for discovery.";
        }

        var packet = BuildArtPollPacket();
        foreach (var target in targets)
        {
            await client.SendAsync(packet, packet.Length, new IPEndPoint(target, settings.ArtnetPort));
        }

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var dedupe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (DateTime.UtcNow < deadline)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(remaining);

            UdpReceiveResult result;
            try
            {
                result = await client.ReceiveAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (!TryParseArtPollReply(result.Buffer, out var node))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(node.IpAddress) || node.IpAddress == "0.0.0.0")
            {
                node.IpAddress = result.RemoteEndPoint.Address.ToString();
            }

            var key = $"{node.IpAddress}:{node.Port}:{node.ShortName}:{node.LongName}";
            if (dedupe.Add(key))
            {
                response.Nodes.Add(node);
            }
        }

        response.Nodes = response.Nodes
            .OrderBy(n => n.IpAddress, StringComparer.OrdinalIgnoreCase)
            .ThenBy(n => n.ShortName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return response;
    }

    private static byte[] BuildArtPollPacket()
    {
        var packet = new byte[14];
        Array.Copy(ArtNetId, packet, ArtNetId.Length);

        packet[8] = (byte)(OpCodeArtPoll & 0xFF);
        packet[9] = (byte)((OpCodeArtPoll >> 8) & 0xFF);
        packet[10] = (byte)((ProtocolVersion >> 8) & 0xFF);
        packet[11] = (byte)(ProtocolVersion & 0xFF);
        packet[12] = 0x00; // TalkToMe
        packet[13] = 0x00; // Priority

        return packet;
    }

    private static bool TryParseArtPollReply(byte[] data, out ArtNetNodeInfo node)
    {
        node = new ArtNetNodeInfo();
        if (data.Length < 26)
        {
            return false;
        }

        if (!IsArtNetPacket(data))
        {
            return false;
        }

        var opCode = (ushort)(data[8] | (data[9] << 8));
        if (opCode != OpCodeArtPollReply)
        {
            return false;
        }

        if (data.Length >= 14)
        {
            var ipBytes = data.AsSpan(10, 4).ToArray();
            node.IpAddress = new IPAddress(ipBytes).ToString();
            node.Port = data[14] | (data[15] << 8);
        }

        node.ShortName = ReadAsciiString(data, 26, 18);
        node.LongName = ReadAsciiString(data, 44, 64);

        return true;
    }

    private static bool IsArtNetPacket(byte[] data)
    {
        if (data.Length < ArtNetId.Length)
        {
            return false;
        }

        for (var i = 0; i < ArtNetId.Length; i++)
        {
            if (data[i] != ArtNetId[i])
            {
                return false;
            }
        }

        return true;
    }

    private static string ReadAsciiString(byte[] data, int offset, int length)
    {
        if (data.Length <= offset)
        {
            return string.Empty;
        }

        var max = Math.Min(length, data.Length - offset);
        var value = Encoding.ASCII.GetString(data, offset, max);
        return value.TrimEnd('\0', ' ');
    }

    private static List<IPAddress> GetBroadcastTargets()
    {
        var targets = new List<IPAddress>
        {
            IPAddress.Broadcast,
            IPAddress.Parse("2.255.255.255")
        };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            IPAddress.Broadcast.ToString(),
            "2.255.255.255"
        };

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            var ipProps = nic.GetIPProperties();
            foreach (var addressInfo in ipProps.UnicastAddresses)
            {
                if (addressInfo.Address.AddressFamily != AddressFamily.InterNetwork)
                {
                    continue;
                }

                if (addressInfo.IPv4Mask is null)
                {
                    continue;
                }

                var ipBytes = addressInfo.Address.GetAddressBytes();
                var maskBytes = addressInfo.IPv4Mask.GetAddressBytes();
                var broadcastBytes = new byte[4];
                for (var i = 0; i < 4; i++)
                {
                    broadcastBytes[i] = (byte)(ipBytes[i] | (byte)~maskBytes[i]);
                }

                var broadcast = new IPAddress(broadcastBytes);
                var key = broadcast.ToString();
                if (seen.Add(key))
                {
                    targets.Add(broadcast);
                }
            }
        }

        return targets;
    }
}
