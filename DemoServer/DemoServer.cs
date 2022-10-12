using CSENet;
using System.Net;

CSENetSocket skt = new();
skt.Bind("127.0.0.1", 18001);

byte[] buffer = new byte[1024];
IPEndPoint? ep = null;

for (int i = 0; i < 10; i++)
{
    int recvLength = skt.Receive(buffer, ref ep);
    Console.WriteLine($"recvLength={recvLength}, data={buffer[0]}");
    Thread.Sleep(1000);
}