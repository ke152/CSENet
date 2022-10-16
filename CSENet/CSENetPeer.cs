using System.Net;

namespace CSENet;

enum CSENetPeerState
{
    Disconnected,
    Connecting,
    AckConnect,
    ConnectionPending,
    ConnectionSucceed,
    Connected,
    DisconnectLater,
    Disconnecting,
    AckDisconnect,
    Zombie,
};

internal class CSENetPeer
{
    public CSENetHost? host;
    public uint outPeerID;
    public uint inPeerID;
    public uint connectID;
    public uint outSessionID;
    public uint inSessionID;
    public IPEndPoint? address;
    public byte[]? data;               /**< Application private data, may be freely modified */
    public CSENetPeerState state;
    public CSENetChannel[]? channels;
    public int channelCount
    {
        get
        {
            return this.channels == null ? 0 : this.channels.Length;
        }
    }
    public uint inBandwidth;  /**< Downstream bandwidth of the client in bytes/second */
    public uint outBandwidth;  /**< Upstream bandwidth of the client in bytes/second */
    public uint inBandwidthThrottleEpoch;
    public uint outBandwidthThrottleEpoch;
    public int inDataTotal;
    public int outDataTotal;
    public long lastSendTime;
    public long lastReceiveTime;
    public long nextTimeout;
    public long earliestTimeout;
    public long packetLossEpoch;
    public uint packetsSent;
    public uint packetsLost;
    public long packetLoss;          /**< mean packet loss of reliable packets as a ratio with respect to the constant ENET_PEER_PACKET_LOSS_SCALE */
    public long packetLossVariance;
    public uint packetThrottle;
    public uint packetThrottleLimit;
    public uint packetThrottleCounter;
    public long packetThrottleEpoch;
    public uint packetThrottleAcceleration;
    public uint packetThrottleDeceleration;
    public uint packetThrottleInterval;
    public uint pingInterval;
    public uint timeoutLimit;
    public long timeoutMinimum;
    public long timeoutMaximum;
    public long lastRoundTripTime;
    public long lowestRoundTripTime;
    public long lastRTTVariance;
    public long highestRoundTripTimeVariance;
    public long rtt;            /**< mean round trip time (RTT), in milliseconds, between sending a reliable packet and receiving its acknowledgement */
    public long rttVariance;
    public uint mtu;
    public uint windowSize;
    public uint reliableDataInTransit;
    public uint outReliableSeqNum;
    public List<CSENetAckCmd> acknowledgements = new();
    public List<CSENetOutCmd> sentReliableCmds = new();
    public List<CSENetOutCmd> sentUnreliableCmds = new();
    public List<CSENetOutCmd> outCmds = new();
    public List<CSENetInCmd> dispatchedCmds = new();
    public bool needDispatch = false;//flags
    public uint inUnseqGroup;
    public uint outUnSeqGroup;
    public uint[] unseqWindow = new uint[CSENetDef.PeerUnseqWindowSize / 32];
    public uint eventData;
    public uint totalWaitingData;
}
