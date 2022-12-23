using System.Runtime.InteropServices;

namespace CSENet;

public enum CSENetProtoFlag
{
    None = 0,
    CmdFlagUnSeq = (1 << 6),
    CmdFlagAck = (1 << 7),

    HeaderFalgCompressed = (1 << 14),
    HeaderFalgSentTime = (1 << 15),
    HeaderFalgMASK = HeaderFalgCompressed | HeaderFalgSentTime,

    HeaderSessionMask = (3 << 12),
    HeaderSessionShift = 12
};

public enum CSENetProtoCmdType
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

public class CSENetProtoHeader
{
    public uint peerID;
    public uint sentTime;
};

public class CSENetProtoCmdHeader
{
    public CSENetProtoCmdType CmdType;
    public CSENetProtoFlag ProtoFlag;
    public uint channelID;
    public uint reliableSeqNum;

    public CSENetProtoCmdHeader()
    {
        this.channelID = 0;
        this.reliableSeqNum = 0;
    }

    public CSENetProtoCmdHeader(CSENetProtoCmdType cmdType, CSENetProtoFlag protoFlag, uint channelID, uint reliableSeqNum)
    {
        this.CmdType = cmdType;
        this.ProtoFlag = protoFlag;
        this.channelID = channelID;
        this.reliableSeqNum = reliableSeqNum;
    }
}

public class CSENetProtoAck
{
    public uint receivedReliableSeqNum = 0;
    public uint receivedSentTime = 0;
};

public class CSENetProtoConnect
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

public class CSENetProtoVerifyConnect
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

public class CSENetProtoBandwidthLimit
{
    public uint inBandwidth = 0;
    public uint outBandwidth = 0;
};

public class CSENetProtoThrottleConfigure
{
    public uint packetThrottleInterval = 0;
    public uint packetThrottleAcceleration = 0;
    public uint packetThrottleDeceleration = 0;
};

public class CSENetProtoDisconnect
{
    public uint data = 0;
};

public class CSENetProtoPing
{
};

public class CSENetProtoSendReliable
{
    public uint dataLength = 0;
};

public class CSENetProtoSendUnReliable
{
    public uint dataLength = 0;
    public uint unreliableSeqNum = 0;
};

public class CSENetProtoSendUnsequenced
{
    public uint unseqGroup = 0;
    public uint dataLength = 0;
};

public class CSENetProtoSendFragment
{
    public uint startSeqNum = 0;
    public uint dataLength = 0;
    public uint fragmentCount = 0;
    public uint fragmentNum = 0;
    public uint totalLength = 0;
    public uint fragmentOffset = 0;
};

public class CSENetProto
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
    {
        this.header = new();
    }

};






