namespace CSENet;

class CSENetAckCmd
{
    public uint sentTime;
    public CSENetProtoCmdHeader cmdHeader = new();
};

class CSENetOutCmd
{
    public uint reliableSeqNum;
    public uint unreliableSeqNum;
    public long sentTime;
    public long rttTimeout;
    public long rttTimeoutLimit;
    public uint fragmentOffset;
    public uint fragmentLength;
    public uint sendAttempts;
    public CSENetProtoCmdHeader cmdHeader = new();
    public CSENetProto cmd = new();
    public CSENetPacket? packet;
};

class CSENetInCmd
{
    public uint reliableSeqNum;
    public uint unreliableSeqNum;
    public CSENetProtoCmdHeader cmdHeader = new();
    public uint fragmentCount;
    public uint fragmentsRemaining;
    public uint[]? fragments;
    public CSENetPacket? packet = null;
};
