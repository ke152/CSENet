using System.Runtime.InteropServices;

namespace CSENet;

enum CSENetProtoFlag
{
    CmdFlagUnSeq = (1 << 6),
    CmdFlagAck = (1 << 7),

    HeaderFalgCompressed = (1 << 14),
    HeaderFalgSentTime = (1 << 15),
    HeaderFalgMASK = HeaderFalgCompressed | HeaderFalgSentTime,

    HeaderSessionMask = (3 << 12),
    HeaderSessionShift = 12
};

enum CSENetProtoCmdType
{
    None = 0,
    Ack = 1,
    Connect = 2,
    VerifyConnect = 3,
    Disconnect = 4,
    Ping = 5,
    SendReliable = 6,
    SendUnreliable = 7,
    SendFragment = 8,
    SendUnseq = 9,
    BandwidthLimit = 10,
    ThrottleConfig = 11,
    SendUnreliableFragment = 12,
    Count = 13,
    Mask = 15
};

class ENetProtoHeader
{
    public uint peerID;
    public uint sentTime;
};

class ENetProtoCmdHeader
{
    public int cmdFlag = 0;
    public uint channelID;
    public uint reliableSeqNum;

    public ENetProtoCmdHeader()
    {
        this.cmdFlag = 0;
        this.channelID = 0;
        this.reliableSeqNum = 0;
    }

    public ENetProtoCmdHeader(int cmdFlag, uint channelID, uint reliableSeqNum)
    {
        this.cmdFlag = cmdFlag;
        this.channelID = channelID;
        this.reliableSeqNum = reliableSeqNum;
    }
}

class ENetProtoAck
{
    public uint receivedReliableSeqNum = 0;
    public uint receivedSentTime = 0;
};

class ENetProtoConnect
{
    public uint outPeerID = 0;
    public uint inSessionID = 0;
    public uint outSessionID = 0;
    public uint mtu = 0;
    public uint windowSize = 0;
    public uint channelCount = 0;
    public uint inBandwidth = 0;
    public uint outBandwidth = 0;
    public uint packetThrottleInterval = 0;
    public uint packetThrottleAcceleration = 0;
    public uint packetThrottleDeceleration = 0;
    public uint connectID = 0;
    public uint data = 0;
};

class ENetProtoVerifyConnect
{
    public uint outPeerID = 0;
    public uint inSessionID = 0;
    public uint outSessionID = 0;
    public uint mtu = 0;
    public uint windowSize = 0;
    public uint channelCount = 0;
    public uint inBandwidth = 0;
    public uint outBandwidth = 0;
    public uint packetThrottleInterval = 0;
    public uint packetThrottleAcceleration = 0;
    public uint packetThrottleDeceleration = 0;
    public uint connectID = 0;
};

class ENetProtoBandwidthLimit
{
    public uint inBandwidth = 0;
    public uint outBandwidth = 0;
};

class ENetProtoThrottleConfigure
{
    public uint packetThrottleInterval = 0;
    public uint packetThrottleAcceleration = 0;
    public uint packetThrottleDeceleration = 0;
};

class ENetProtoDisconnect
{
    public uint data = 0;
};

class ENetProtoPing
{
};

class ENetProtoSendReliable
{
    public uint dataLength = 0;
};

class ENetProtoSendUnReliable
{
    public uint dataLength = 0;
    public uint unreliableSeqNum = 0;
};

class ENetProtoSendUnsequenced
{
    public uint unseqGroup = 0;
    public uint dataLength = 0;
};

class ENetProtoSendFragment
{
    public uint startSeqNum = 0;
    public uint dataLength = 0;
    public uint fragmentCount = 0;
    public uint fragmentNum = 0;
    public uint totalLength = 0;
    public uint fragmentOffset = 0;
};

internal class ENetProto
{
    public ENetProtoCmdHeader header;
    public ENetProtoAck? ack;
    public ENetProtoConnect? connect;
    public ENetProtoVerifyConnect? verifyConnect;
    public ENetProtoDisconnect? disconnect;
    public ENetProtoPing? ping;
    public ENetProtoSendReliable? sendReliable;
    public ENetProtoSendUnReliable? sendUnReliable;
    public ENetProtoSendUnsequenced? sendUnseq;
    public ENetProtoSendFragment? sendFragment;
    public ENetProtoBandwidthLimit? bandwidthLimit;
    public ENetProtoThrottleConfigure? throttleConfigure;

    public ENetProto()
    {//TODO:这个构造函数可能需要改？不需要所有的都new出来吧？
        this.header = new();
    }
}

static class ENetProtoCmdSize
{
    public static List<uint> CmdSize = Init();

    private static List<uint> Init()
    {
        List<uint> cmdSizeList = new List<uint>();

        cmdSizeList.Add(0);
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf<ENetProtoAck>()));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf(new ENetProtoConnect())));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf(new ENetProtoVerifyConnect())));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf(new ENetProtoDisconnect())));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf(new ENetProtoPing())));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf(new ENetProtoSendReliable())));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf(new ENetProtoSendUnReliable())));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf(new ENetProtoSendUnsequenced())));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf(new ENetProtoSendUnsequenced())));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf(new ENetProtoBandwidthLimit())));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf(new ENetProtoThrottleConfigure())));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf(new ENetProtoSendFragment())));

        return cmdSizeList;
    }
};






