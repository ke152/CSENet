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
