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

class CSENetProtoHeader
{
    public uint peerID;
    public uint sentTime;
};

class CSENetProtoCmdHeader
{
    public int cmdFlag = 0;
    public uint channelID;
    public uint reliableSeqNum;

    public CSENetProtoCmdHeader()
    {
        this.cmdFlag = 0;
        this.channelID = 0;
        this.reliableSeqNum = 0;
    }

    public CSENetProtoCmdHeader(int cmdFlag, uint channelID, uint reliableSeqNum)
    {
        this.cmdFlag = cmdFlag;
        this.channelID = channelID;
        this.reliableSeqNum = reliableSeqNum;
    }
}

class CSENetProtoAck
{
    public uint receivedReliableSeqNum = 0;
    public uint receivedSentTime = 0;
};

class CSENetProtoConnect
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

class CSENetProtoVerifyConnect
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

class CSENetProtoBandwidthLimit
{
    public uint inBandwidth = 0;
    public uint outBandwidth = 0;
};

class CSENetProtoThrottleConfigure
{
    public uint packetThrottleInterval = 0;
    public uint packetThrottleAcceleration = 0;
    public uint packetThrottleDeceleration = 0;
};

class CSENetProtoDisconnect
{
    public uint data = 0;
};

class CSENetProtoPing
{
};

class CSENetProtoSendReliable
{
    public uint dataLength = 0;
};

class CSENetProtoSendUnReliable
{
    public uint dataLength = 0;
    public uint unreliableSeqNum = 0;
};

class CSENetProtoSendUnsequenced
{
    public uint unseqGroup = 0;
    public uint dataLength = 0;
};

class CSENetProtoSendFragment
{
    public uint startSeqNum = 0;
    public uint dataLength = 0;
    public uint fragmentCount = 0;
    public uint fragmentNum = 0;
    public uint totalLength = 0;
    public uint fragmentOffset = 0;
};

internal class CSENetProto
{
    public CSENetProtoCmdHeader header;
    public CSENetProtoAck? ack;
    public CSENetProtoConnect? connect;
    public CSENetProtoVerifyConnect? verifyConnect;
    public CSENetProtoDisconnect? disconnect;
    public CSENetProtoPing? ping;
    public CSENetProtoSendReliable? sendReliable;
    public CSENetProtoSendUnReliable? sendUnReliable;
    public CSENetProtoSendUnsequenced? sendUnseq;
    public CSENetProtoSendFragment? sendFragment;
    public CSENetProtoBandwidthLimit? bandwidthLimit;
    public CSENetProtoThrottleConfigure? throttleConfigure;

    public CSENetProto()
    {//TODO:这个构造函数可能需要改？不需要所有的都new出来吧？
        this.header = new();
    }
}

static class CSENetProtoCmdSize
{
    public static List<uint> CmdSize = Init();

    private static List<uint> Init()
    {
        List<uint> cmdSizeList = new List<uint>();

        cmdSizeList.Add(0);
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf<CSENetProtoAck>()));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf<CSENetProtoConnect>()));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf<CSENetProtoVerifyConnect>()));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf<CSENetProtoDisconnect>()));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf<CSENetProtoPing>()));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf<CSENetProtoSendReliable>()));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf<CSENetProtoSendUnReliable>()));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf<CSENetProtoSendUnsequenced>()));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf<CSENetProtoBandwidthLimit>()));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf<CSENetProtoThrottleConfigure>()));
        cmdSizeList.Add(Convert.ToUInt32(Marshal.SizeOf<CSENetProtoSendFragment>()));

        return cmdSizeList;
    }
};






