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
    public int ChannelCount
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

    public void Reset()
    {
        OnDisconnect();

        this.outPeerID = CSENetDef.ProtoMaxPeerID;
        this.connectID = 0;

        this.state = CSENetPeerState.Disconnected;

        this.inBandwidth = 0;
        this.outBandwidth = 0;
        this.inBandwidthThrottleEpoch = 0;
        this.outBandwidthThrottleEpoch = 0;
        this.inDataTotal = 0;
        this.outDataTotal = 0;
        this.lastSendTime = 0;
        this.lastReceiveTime = 0;
        this.nextTimeout = 0;
        this.earliestTimeout = 0;
        this.packetLossEpoch = 0;
        this.packetsSent = 0;
        this.packetsLost = 0;
        this.packetLoss = 0;
        this.packetLossVariance = 0;
        this.packetThrottle = CSENetDef.PeerDefaultPacketThrottle;
        this.packetThrottleLimit = CSENetDef.PeerPacketThrottleScale;
        this.packetThrottleCounter = 0;
        this.packetThrottleEpoch = 0;
        this.packetThrottleAcceleration = CSENetDef.PeerPacketThrottleAcceleration;
        this.packetThrottleDeceleration = CSENetDef.PeerPacketThrottleDeceleration;
        this.packetThrottleInterval = CSENetDef.PeerPacketThrottleInterval;
        this.pingInterval = CSENetDef.PeerPingInterval;
        this.timeoutLimit = CSENetDef.PeerTimeoutLimit;
        this.timeoutMinimum = CSENetDef.PeerTimeoutMin;
        this.timeoutMaximum = CSENetDef.PeerTimeoutMax;
        this.lastRoundTripTime = CSENetDef.PeerDefaultRTT;
        this.lowestRoundTripTime = CSENetDef.PeerDefaultRTT;
        this.lastRTTVariance = 0;
        this.highestRoundTripTimeVariance = 0;
        this.rtt = CSENetDef.PeerDefaultRTT;
        this.rttVariance = 0;
        this.mtu = this.host == null ? 0 : this.host.mtu;
        this.reliableDataInTransit = 0;
        this.outReliableSeqNum = 0;
        this.windowSize = CSENetDef.ProtoMaxWindowSize;
        this.inUnseqGroup = 0;
        this.outUnSeqGroup = 0;
        this.eventData = 0;
        this.totalWaitingData = 0;
        this.needDispatch = false;

        Array.Clear(this.unseqWindow);

        ResetQueues();
    }

    public void OnDisconnect()
    {
        if (this.host == null)
            return;

        if (state == CSENetPeerState.Connected || state == CSENetPeerState.DisconnectLater)
        {
            if (inBandwidth != 0)
            {
                this.host.bandwidthLimitedPeers--;
            }

            this.host.connectedPeers--;
        }
    }

    public void ResetQueues()
    {
        if (this.host != null)
        {
            this.host.dispatchQueue.Remove(this);
        }

        needDispatch = false;

        acknowledgements.Clear();
        sentReliableCmds.Clear();
        sentUnreliableCmds.Clear();
        outCmds.Clear();
        dispatchedCmds.Clear();

        channels = null;
    }
}
