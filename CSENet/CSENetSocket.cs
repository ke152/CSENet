using System.Net;
using System.Net.Sockets;

namespace CSENet;

public enum CSENetSocketOptType
{
    NonBlock = 1,
    Broadcast = 2,
    RcvBuf = 3,
    SendBuf = 4,
    ReuseAddr = 5,
    RcvTimeout = 6,
    SendTimeout = 7,
    Error = 8,
    NoDelay = 9
}

public enum CSENetSocketWait
{
    None = 0,
    Send = (1 << 0),
    Recv = (1 << 1),
    Interrupt = (1 << 2)
}

public class CSENetSocket
{
    public Socket socket;
    public IPEndPoint? localIP;

    public CSENetSocket()
    {
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    }

    ~CSENetSocket()
    {
        socket.Close();
    }

    public void SetOption(CSENetSocketOptType type, int value)
    {
        switch (type)
        {
            case CSENetSocketOptType.NonBlock:
                //TODO: nonblock？
                //socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, value);
                //socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, value);
                //socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.UnblockSource, value);
                break;
            case CSENetSocketOptType.Broadcast:
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, value);
                break;
            case CSENetSocketOptType.RcvBuf:
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, value);
                break;
            case CSENetSocketOptType.SendBuf:
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, value);
                break;
            case CSENetSocketOptType.ReuseAddr:
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, value);
                break;
            case CSENetSocketOptType.RcvTimeout:
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, value);
                break;
            case CSENetSocketOptType.SendTimeout:
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, value);
                break;
            case CSENetSocketOptType.Error:
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error, value);
                break;
            case CSENetSocketOptType.NoDelay:
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, value);
                break;
            default:
                break;
        }
    }

    public void Shutdown(SocketShutdown how)
    {
        socket.Shutdown(how);
    }

    public void Bind(string ip, int port)
    {
        localIP = new IPEndPoint(IPAddress.Parse(ip), port);
        socket.Bind(localIP);//TODO：try-catch
    }

    public void Bind(IPEndPoint ep)
    {
        socket.Bind(ep);//TODO：try-catch
        localIP = ep;
    }

    public IPEndPoint? GetAddress()
    {
        if (localIP == null)
        {
            localIP = socket.LocalEndPoint as IPEndPoint;
        }
        return localIP;
    }

    public bool Wait(Int32 microSecondsTimeout, SelectMode mode)
    {
        return socket.Poll(microSecondsTimeout, mode);
    }

    public int SendTo(IPEndPoint? ep, List<byte[]> buffers)//TODO:批量发送，而不是一个一个发
    {
        if (ep == null) return 0;

        int sentLength = 0;
        for (int i = 0; i < buffers.Count; i++)
        {
            try
            {
                sentLength += this.socket.SendTo(buffers[i], ep);
            }
            catch (Exception)
            {
                Console.WriteLine("Socket send failed");
            }
        }
        Console.WriteLine($"Socket send length={sentLength}");
        return sentLength;
    }

    public int Receive(byte[] buffer, ref IPEndPoint? ep)
    {
        try
        {
            int length = socket.Receive(buffer);
            ep = socket.RemoteEndPoint as IPEndPoint;
            Console.WriteLine($"socket recv length:{length}");
            return length;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return 0;
        }
    }


}
