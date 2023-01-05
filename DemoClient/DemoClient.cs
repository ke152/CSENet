using CSENet;
using System.Net;
using NLog;

var logger = LogManager.GetCurrentClassLogger();

CSENetHost host = new();
host.Create(null, 3, 3, 0, 0);

IPEndPoint serverEP = new(IPAddress.Parse("127.0.0.1"), 18001);
var peer = host.Connect(serverEP, 3, 0);

if (peer == null)
{
    Console.WriteLine("connect fail, peer is null");
    return;
}

byte[] data = new byte[] { 1, 2, 3 };
CSENetPacketFlag flags = CSENetPacketFlag.None;
CSENetPacket packet = new CSENetPacket(data, flags);
peer.Send(0, packet);


CSENetEvent? e = null;
long timeout = 1000;
while (host.HostService(e, timeout) > 0)
{
    switch (e?.type)
    {
        case CSENetEventType.None:
            Console.WriteLine(e.type);
            break;
        case CSENetEventType.Connect:
            Console.WriteLine(e.type);
            break;
        case CSENetEventType.Disconnect:
            Console.WriteLine(e.type);
            break;
        case CSENetEventType.Recv:
            Console.WriteLine(e.type);
            break;
        default:
            Console.WriteLine(e.type);
            break;
    }
    Thread.Sleep(1000);
}
