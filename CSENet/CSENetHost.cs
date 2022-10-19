using System.Net;

namespace CSENet;

public class CSENetHost
{
    public CSENetSocket? socket;
    public IPEndPoint? address;                     /**< Internet address of the host */
    public uint inBandwidth;           /**< downstream bandwidth of the host */
    public uint outBandwidth;           /**< upstream bandwidth of the host */
    public uint bandwidthThrottleEpoch;
    public uint mtu;
    public uint randomSeed;
    public int recalculateBandwidthLimits;
    public CSENetPeer[]? peers;                       /**< array of peers allocated for this host */
    public uint peerCount;                   /**< number of peers allocated for this host */
    public uint channelLimit;                /**< maximum number of channels allowed for connected peers */
    public long serviceTime;
    public List<CSENetPeer> dispatchQueue = new();
    public int continueSending;
    public uint packetSize;
    public uint headerFlags;
    public List<CSENetProto> commands = new();// 不能超过：ENetDef.ProtoMaxPacketCmds
    public int CommandCount { get { return this.commands.Count; } }
    public List<byte[]> buffers = new();//不能超过： ENetDef.BufferMax
    public int BufferCount { get { return this.buffers.Count; } }
    public byte[]?[]? packetData;
    public IPEndPoint? receivedAddress;
    public byte[]? receivedData;
    public int receivedDataLength;
    public uint totalSentData;               /**< total data sent, user should reset to 0 as needed to prevent overflow */
    public uint totalSentPackets;            /**< total UDP packets sent, user should reset to 0 as needed to prevent overflow */
    public int totalReceivedData;           /**< total data received, user should reset to 0 as needed to prevent overflow */
    public uint totalReceivedPackets;        /**< total UDP packets received, user should reset to 0 as needed to prevent overflow */
    public uint connectedPeers;
    public uint bandwidthLimitedPeers;
    public uint duplicatePeers;              /**< optional number of allowed peers from duplicate IPs, defaults to Proto_MAXIMUMPEERID */
    public uint maximumPacketSize;           /**< the maximum allowable packet size that may be sent or received on a peer */
    public uint maximumWaitingData;          /**< the maximum aggregate amount of buffer space a peer may use waiting for packets to be delivered */

    public CSENetHost()
    {
        this.packetData = new byte[2][];
        this.packetData[0] = new byte[CSENetDef.ProtoMaxMTU];
        this.packetData[1] = new byte[CSENetDef.ProtoMaxMTU];
    }

    public void Create(IPEndPoint? address, uint peerCount, uint channelLimit, uint incomingBandwidth, uint outgoingBandwidth)
    {
        if (peerCount > CSENetDef.ProtoMaxPeerID)
            return;

        this.peers = new CSENetPeer[peerCount];
        for (int i = 0; i < peerCount; i++)
        {
            this.peers[i] = new CSENetPeer();
        }

        this.socket = new CSENetSocket();

        if (address != null)
        {
            this.socket.Bind(address);
        }

        this.socket.SetOption(CSENetSocketOptType.NonBlock, 1);
        this.socket.SetOption(CSENetSocketOptType.Broadcast, 1);
        this.socket.SetOption(CSENetSocketOptType.RcvBuf, (int)CSENetDef.HostRecvBufferSize);
        this.socket.SetOption(CSENetSocketOptType.SendBuf, (int)CSENetDef.HostSendBufferSize);
        this.socket.SetOption(CSENetSocketOptType.RcvTimeout, 1000);
        this.socket.SetOption(CSENetSocketOptType.SendTimeout, 1000);

        this.address = address;

        if (channelLimit != 0 || channelLimit > CSENetDef.ProtoMaxChannelCount)
            channelLimit = CSENetDef.ProtoMaxChannelCount;
        else
        if (channelLimit < CSENetDef.ProtoMinChannelCount)
            channelLimit = CSENetDef.ProtoMinChannelCount;

        this.randomSeed = (uint)(new Random()).Next(0);
        this.randomSeed += CSENetUtils.RandomSeed();
        this.randomSeed = (this.randomSeed << 16) | (this.randomSeed >> 16);
        this.channelLimit = channelLimit;
        this.inBandwidth = incomingBandwidth;
        this.outBandwidth = outgoingBandwidth;
        this.bandwidthThrottleEpoch = 0;
        this.recalculateBandwidthLimits = 0;
        this.mtu = CSENetDef.HostDefaultMTU;
        this.peerCount = peerCount;
        this.receivedAddress = new IPEndPoint(IPAddress.Any, 0);
        this.receivedData = null;
        this.receivedDataLength = 0;

        this.totalSentData = 0;
        this.totalSentPackets = 0;
        this.totalReceivedData = 0;
        this.totalReceivedPackets = 0;

        this.connectedPeers = 0;
        this.bandwidthLimitedPeers = 0;
        this.duplicatePeers = CSENetDef.ProtoMaxPeerID;
        this.maximumPacketSize = CSENetDef.HostDefaultMaxPacketSize;
        this.maximumWaitingData = CSENetDef.HostDefaultMaxWaintingData;

        this.dispatchQueue.Clear();

        for (uint i = 0; i < this.peers.Length; i++)
        {
            var currentPeer = this.peers[i];
            currentPeer.inPeerID = i;
            currentPeer.outSessionID = currentPeer.inSessionID = 0xFF;
            currentPeer.data = null;

            currentPeer.acknowledgements.Clear();
            currentPeer.sentReliableCmds.Clear();
            currentPeer.sentUnreliableCmds.Clear();
            currentPeer.outCmds.Clear();
            currentPeer.dispatchedCmds.Clear();

            currentPeer.Reset();
        }
    }

    public void Destroy()
    {
        this.socket = null;

        if (this.peers == null) 
            return;

        foreach (var peer in this.peers)
        {
            peer.Reset();
        }

        this.peers = null;
    }

    public uint Random()
    {
        uint n = (this.randomSeed += 0x6D2B79F5U);
        n = (n ^ (n >> 15)) * (n | 1U);
        n ^= n + (n ^ (n >> 7)) * (n | 61U);
        return n ^ (n >> 14);
    }

    public void ChannelLimit(uint channelLimit)
    {
        if (channelLimit > 0 || channelLimit > CSENetDef.ProtoMaxChannelCount)
            channelLimit = CSENetDef.ProtoMaxChannelCount;
        else if (channelLimit < CSENetDef.ProtoMinChannelCount)
                channelLimit = CSENetDef.ProtoMinChannelCount;

        this.channelLimit = channelLimit;
    }


    public void BandwidthLimit(uint incomingBandwidth, uint outgoingBandwidth)
    {
        this.inBandwidth = incomingBandwidth;
        this.outBandwidth = outgoingBandwidth;
        this.recalculateBandwidthLimits = 1;
    }






}
