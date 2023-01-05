using CSENet;
using System.Net;
using NLog;

var logger = LogManager.GetCurrentClassLogger();

CSENetHost host = new();
host.Create(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 19001), 3, 3, 0, 0);

CSENetEvent? e=null;
long timeout = 1000;
while (host.HostService(e, timeout) > 0)
{
    switch (e?.type)
    {
    case CSENetEventType.Connect:
        Console.WriteLine($"A new client connected from {e.peer?.address?.Address.ToString()}:{e.peer?.address?.Port}\n");
        break;
    case CSENetEventType.Recv:
            Console.WriteLine($"A packet of length {e.packet?.Data?.Length} containing {e.packet?.Data} was received from one of client on channel {e.channelID}.\n");
        break;
       
    case CSENetEventType.Disconnect:
            Console.WriteLine("one of client disconnected.\n");
            break;
        default:
            Console.WriteLine($"error CSENetEventType:{e?.type}");
            break;
    }
}

