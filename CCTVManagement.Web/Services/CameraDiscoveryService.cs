using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CCTVManagement.Web.Services;

public class CameraDiscoveryService
{
    public async Task<List<string>> DiscoverImouCamerasAsync(int timeoutSeconds = 3)
    {
        var discoveredIps = new HashSet<string>();
        using var udp = new UdpClient();
        udp.EnableBroadcast = true;

        string probeXml = @"
            <e:Envelope xmlns:e=""http://www.w3.org/2003/05/soap-envelope""
                        xmlns:w=""http://schemas.xmlsoap.org/ws/2004/08/addressing""
                        xmlns:d=""http://schemas.xmlsoap.org/ws/2005/04/discovery"">
                <e:Header>
                    <w:MessageID>uuid:" + Guid.NewGuid() + @"</w:MessageID>
                    <w:To>urn:schemas-xml-org:ws:2005:04:discovery</w:To>
                    <w:Action>http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe</w:Action>
                </e:Header>
                <e:Body>
                    <d:Probe>
                        <d:Types>dn:NetworkVideoTransmitter</d:Types>
                    </d:Probe>
                </e:Body>
            </e:Envelope>";

        byte[] payload = Encoding.UTF8.GetBytes(probeXml);
        var target = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 3702);
        await udp.SendAsync(payload, payload.Length, target);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        while (!cts.IsCancellationRequested)
        {
            try
            {
                var result = await udp.ReceiveAsync(cts.Token);
                string ip = result.RemoteEndPoint.Address.ToString();
                discoveredIps.Add(ip);
            }
            catch (OperationCanceledException) { break; }
        }

        return discoveredIps.ToList();
    }
}