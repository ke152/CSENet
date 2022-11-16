using System.Net;
using System.Runtime.InteropServices;

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


    public void BandwidthThrottle()
    {
        uint timeCurrent = (uint)CSENetUtils.TimeGet();
        uint elapsedTime = timeCurrent - this.bandwidthThrottleEpoch,
           peersRemaining = (uint)this.connectedPeers,
           bandwidth = uint.MaxValue,
           throttle,
           bandwidthLimit = 0;
        int dataTotal = int.MaxValue;
        int needsAdjustment = this.bandwidthLimitedPeers > 0 ? 1 : 0;

        if (elapsedTime < CSENetDef.HostBandwidthThrottleInterval)
            return;

        this.bandwidthThrottleEpoch = timeCurrent;

        if (peersRemaining == 0 || this.peers == null)
            return;

        if (this.outBandwidth != 0)
        {
            dataTotal = 0;
            bandwidth = (this.outBandwidth * elapsedTime) / 1000;

            if (this.peers != null)
            {
                foreach (var peer in this.peers)
                {
                    if (peer.state != CSENetPeerState.Connected && peer.state != CSENetPeerState.DisconnectLater)
                        continue;

                    dataTotal += peer.outDataTotal;
                }
            }
        }

        while (peersRemaining > 0 && needsAdjustment != 0)
        {
            needsAdjustment = 0;

            if (dataTotal <= bandwidth)
                throttle = CSENetDef.PeerPacketThrottleScale;
            else
                throttle = (bandwidth * CSENetDef.PeerPacketThrottleScale) / (uint)dataTotal;

            if (this.peers != null)
            {
                foreach (var peer in this.peers)
                {
                    uint peerBandwidth;

                    if ((peer.state != CSENetPeerState.Connected && peer.state != CSENetPeerState.DisconnectLater) ||
                        peer.inBandwidth == 0 ||
                        peer.outBandwidthThrottleEpoch == timeCurrent)
                        continue;

                    peerBandwidth = (peer.inBandwidth * elapsedTime) / 1000;
                    if ((throttle * peer.outDataTotal) / CSENetDef.PeerPacketThrottleScale <= peerBandwidth)
                        continue;

                    peer.packetThrottleLimit = (peerBandwidth *
                                                    CSENetDef.PeerPacketThrottleScale) / (uint)peer.outDataTotal;

                    if (peer.packetThrottleLimit == 0)
                        peer.packetThrottleLimit = 1;

                    if (peer.packetThrottle > peer.packetThrottleLimit)
                        peer.packetThrottle = peer.packetThrottleLimit;

                    peer.outBandwidthThrottleEpoch = timeCurrent;

                    peer.inDataTotal = 0;
                    peer.outDataTotal = 0;

                    needsAdjustment = 1;
                    --peersRemaining;
                    bandwidth -= peerBandwidth;
                    dataTotal -= (int)peerBandwidth;
                }
            }
        }

        if (peersRemaining > 0)
        {
            if (dataTotal <= bandwidth)
                throttle = CSENetDef.PeerPacketThrottleScale;
            else
                throttle = (bandwidth * CSENetDef.PeerPacketThrottleScale) / (uint)dataTotal;

            if (this.peers != null)
            {
                foreach (var peer in this.peers)
                {
                    if ((peer.state != CSENetPeerState.Connected && peer.state != CSENetPeerState.DisconnectLater) ||
                        peer.outBandwidthThrottleEpoch == timeCurrent)
                        continue;

                    peer.packetThrottleLimit = throttle;

                    if (peer.packetThrottle > peer.packetThrottleLimit)
                        peer.packetThrottle = peer.packetThrottleLimit;

                    peer.inDataTotal = 0;
                    peer.outDataTotal = 0;
                }
            }
        }

        if (this.recalculateBandwidthLimits != 0)
        {
            this.recalculateBandwidthLimits = 0;

            peersRemaining = (uint)this.connectedPeers;
            bandwidth = this.inBandwidth;
            needsAdjustment = 1;

            if (bandwidth == 0)
                bandwidthLimit = 0;
            else
                while (peersRemaining > 0 && needsAdjustment != 0)
                {
                    needsAdjustment = 0;
                    bandwidthLimit = bandwidth / peersRemaining;

                    if (this.peers == null) break;

                    foreach (var peer in this.peers)
                    {
                        if ((peer.state != CSENetPeerState.Connected && peer.state != CSENetPeerState.DisconnectLater) ||
                            peer.inBandwidthThrottleEpoch == timeCurrent)
                            continue;

                        if (peer.outBandwidth > 0 &&
                            peer.outBandwidth >= bandwidthLimit)
                            continue;

                        peer.inBandwidthThrottleEpoch = timeCurrent;

                        needsAdjustment = 1;
                        --peersRemaining;
                        bandwidth -= peer.outBandwidth;
                    }
                }

            if (this.peers != null)
            {
                foreach (var peer in this.peers)
                {
                    if (peer.state != CSENetPeerState.Connected && peer.state != CSENetPeerState.DisconnectLater)
                        continue;

                    CSENetProto command = new();
                    command.header.cmdFlag = (int)CSENetProtoCmdType.BandwidthLimit | (int)CSENetProtoFlag.CmdFlagAck;
                    command.header.channelID = 0xFF;
                    command.bandwidthLimit = new();
                    command.bandwidthLimit.outBandwidth = (uint)IPAddress.HostToNetworkOrder(this.outBandwidth);

                    if (peer.inBandwidthThrottleEpoch == timeCurrent)
                        command.bandwidthLimit.inBandwidth = (uint)IPAddress.HostToNetworkOrder(peer.outBandwidth);
                    else
                        command.bandwidthLimit.inBandwidth = (uint)IPAddress.HostToNetworkOrder(bandwidthLimit);

                    peer.QueueOutgoingCommand(command, null, 0, 0);
                }
            }
        }
    }

    public void Flush()
    {
        this.serviceTime = CSENetUtils.TimeGet();
        ProtoSendOutCmds(null, 0);
    }

    public CSENetPeer? Connect(IPEndPoint address, uint channelCount, uint data)
    {
        CSENetPeer? currentPeer = null;

        if (channelCount < (int)CSENetDef.ProtoMinChannelCount)
            channelCount = (int)CSENetDef.ProtoMinChannelCount;
        else
        if (channelCount > (int)CSENetDef.ProtoMaxChannelCount)
            channelCount = (int)CSENetDef.ProtoMaxChannelCount;

        if (this.peers == null)
        {
            return null;
        }

        foreach (var tmpPeer in this.peers)
        {
            if (tmpPeer.state == CSENetPeerState.Disconnected)
            {
                currentPeer = tmpPeer;
                break;
            }
        }

        if (currentPeer == null)
            return null;

        currentPeer.channels = new CSENetChannel[channelCount];
        for (int i = 0; i < currentPeer.channels.Length; i++)
        {
            currentPeer.channels[i] = new();
        }

        currentPeer.state = CSENetPeerState.Connecting;
        currentPeer.address = address;
        currentPeer.connectID = Random();

        if (this.outBandwidth == 0)
            currentPeer.windowSize = (int)CSENetDef.ProtoMaxWindowSize;
        else
            currentPeer.windowSize = (this.outBandwidth /
                                          (uint)CSENetDef.PeerWindowSizeScale) *
                                            (int)CSENetDef.ProtoMinWindowSize;

        if (currentPeer.windowSize < (int)CSENetDef.ProtoMinWindowSize)
            currentPeer.windowSize = (int)CSENetDef.ProtoMinWindowSize;
        else
        if (currentPeer.windowSize > (int)CSENetDef.ProtoMaxWindowSize)
            currentPeer.windowSize = (int)CSENetDef.ProtoMaxWindowSize;

        if (currentPeer.channels != null)
        {
            for (int i = 0; i < currentPeer.channels.Length; i++)
            {
                var channel = currentPeer.channels[i];

                channel.outReliableSeqNum = 0;
                channel.outUnreliableSeqNum = 0;
                channel.inReliableSeqNum = 0;
                channel.inUnreliableSeqNum = 0;

                channel.inReliableCmds.Clear();
                channel.inUnreliableCmds.Clear();

                channel.usedReliableWindows = 0;
            }
        }

        CSENetProto command = new();
        command.header.cmdFlag = (int)CSENetProtoCmdType.Connect | (int)CSENetProtoFlag.CmdFlagAck;
        command.header.channelID = 0xFF;
        command.connect = new();
        command.connect.outPeerID = (uint)IPAddress.HostToNetworkOrder(currentPeer.inPeerID);
        if (currentPeer != null)
        {
            command.connect.inSessionID = currentPeer.inSessionID;
            command.connect.outSessionID = currentPeer.outSessionID;
            command.connect.mtu = (uint)IPAddress.HostToNetworkOrder(currentPeer.mtu);
            command.connect.windowSize = (uint)IPAddress.HostToNetworkOrder(currentPeer.windowSize);
            command.connect.packetThrottleInterval = (uint)IPAddress.HostToNetworkOrder(currentPeer.packetThrottleInterval);
            command.connect.packetThrottleAcceleration = (uint)IPAddress.HostToNetworkOrder(currentPeer.packetThrottleAcceleration);
            command.connect.packetThrottleDeceleration = (uint)IPAddress.HostToNetworkOrder(currentPeer.packetThrottleDeceleration);
            command.connect.connectID = currentPeer.connectID;
        }
        command.connect.channelCount = (uint)IPAddress.HostToNetworkOrder(channelCount);
        command.connect.inBandwidth = (uint)IPAddress.HostToNetworkOrder(this.inBandwidth);
        command.connect.outBandwidth = (uint)IPAddress.HostToNetworkOrder(this.outBandwidth);
        command.connect.data = (uint)IPAddress.HostToNetworkOrder(data);

        currentPeer?.QueueOutgoingCommand(command, null, 0, 0);

        return currentPeer;
    }

    public void Broadcast(uint channelID, CSENetPacket packet)
    {
        if (this.peers == null)
            return;

        foreach (var currentPeer in this.peers)
        {
            if (currentPeer.state != CSENetPeerState.Connected)
                continue;

            currentPeer.Send(channelID, packet);
        }
    }

    public int CheckEvents(CSENetEvent? @event)
    {
        if (@event == null) return -1;

        @event.type = CSENetEventType.None;
        @event.peer = null;
        @event.packet = null;

        return ProtoDispatchIncomingCommands(ref @event);
    }

    #region proto

    public void ProtoChangeState(CSENetPeer peer, CSENetPeerState state)
    {
        if (state == CSENetPeerState.Connected || state == CSENetPeerState.DisconnectLater)
            peer.OnConnect();
        else
            peer.OnDisconnect();

        peer.state = state;
    }


    public void ProtoDispatchState(CSENetPeer peer, CSENetPeerState state)
    {
        ProtoChangeState(peer, state);

        if (!(peer.needDispatch))
        {
            this.dispatchQueue.Add(peer);
            peer.needDispatch = true;
        }
    }

    public int ProtoDispatchIncomingCommands(ref CSENetEvent? @event)
    {
        if (this.dispatchQueue == null) return -1;

        while (this.dispatchQueue.Count != 0)
        {
            CSENetPeer? peer = this.dispatchQueue[0];
            if (peer == null)
                continue;

            peer.needDispatch = false;

            switch (peer.state)
            {
                case CSENetPeerState.ConnectionPending:
                case CSENetPeerState.ConnectionSucceed:
                    ProtoChangeState(peer, CSENetPeerState.Connected);
                    if (@event != null)
                    {
                        @event.type = CSENetEventType.Connect;
                        @event.peer = peer;
                        @event.data = peer.eventData;
                    }
                    return 1;
                case CSENetPeerState.Zombie:
                    this.recalculateBandwidthLimits = 1;

                    if (@event != null)
                    {
                        @event.type = CSENetEventType.Disconnect;
                        @event.peer = peer;
                        @event.data = peer.@eventData;
                    }

                    peer.Reset();
                    return 1;

                case CSENetPeerState.Connected:
                    if (peer.dispatchedCmds.Count == 0)
                        continue;

                    if (@event != null)
                    {
                        @event.packet = peer.Receive(ref @event.channelID);
                        if (@event.packet == null)
                            continue;

                        @event.type = CSENetEventType.Recv;
                        @event.peer = peer;
                    }

                    if (peer.dispatchedCmds.Count != 0)
                    {
                        peer.needDispatch = true;

                        this.dispatchQueue.Add(peer);
                    }

                    return 1;

                default:
                    break;
            }
        }

        return 0;
    }

    public void ProtoNotifyConnect(CSENetPeer peer, CSENetEvent? @event)
    {
        this.recalculateBandwidthLimits = 1;

        if (@event != null)
        {
            ProtoChangeState(peer, CSENetPeerState.Connected);

            @event.type = CSENetEventType.Connect;
            @event.peer = peer;
            @event.data = peer.@eventData;
        }
        else
            ProtoDispatchState(peer, peer.state == CSENetPeerState.Connecting ? CSENetPeerState.ConnectionSucceed : CSENetPeerState.ConnectionPending);
    }

    public void ProtoRemoveSentUnreliableCommands(CSENetPeer peer)
    {
        if (peer.sentUnreliableCmds.Count == 0)
            return;

        peer.sentUnreliableCmds?.Clear();

        if (peer.state == CSENetPeerState.DisconnectLater &&
            peer.outCmds.Count == 0 &&
            peer.sentReliableCmds.Count == 0)
            peer.Disconnect(peer.@eventData);
    }

    public void ProtoNotifyDisconnect(CSENetPeer peer, CSENetEvent? @event)
    {
        if (peer.state >= CSENetPeerState.ConnectionPending)
            this.recalculateBandwidthLimits = 1;

        if (peer.state != CSENetPeerState.Connecting && peer.state < CSENetPeerState.ConnectionSucceed)
            peer.Reset();
        else
        {
            if (@event != null)
            {
                @event.type = CSENetEventType.Disconnect;
                @event.peer = peer;
                @event.data = 0;

                peer.Reset();
            }
            else
            {
                peer.@eventData = 0;

                ProtoDispatchState(peer, CSENetPeerState.Zombie);
            }
        }
    }

    public CSENetProtoCmdType ProtoRemoveSentReliableCommand(CSENetPeer peer, uint reliableSequenceNumber, uint channelID)
    {
        CSENetOutCmd? outgoingCommand = null;
        CSENetProtoCmdType commandNumber = CSENetProtoCmdType.None;
        int wasSent = 1;

        foreach (var currentCommand in peer.sentReliableCmds)
        {
            outgoingCommand = currentCommand;

            if (outgoingCommand?.reliableSeqNum == reliableSequenceNumber &&
                outgoingCommand?.cmdHeader.channelID == channelID)
            {
                peer.sentReliableCmds.Remove(currentCommand);
                break;
            }
        }

        if (outgoingCommand == null)
        {
            foreach (var currentCommand in peer.outCmds)
            {
                outgoingCommand = currentCommand;

                if ((outgoingCommand?.cmdHeader.cmdFlag & (int)CSENetProtoFlag.CmdFlagAck) != 0)
                    continue;

                if (outgoingCommand?.sendAttempts < 1) return (int)CSENetProtoCmdType.None;

                if (outgoingCommand?.reliableSeqNum == reliableSequenceNumber &&
                    outgoingCommand?.cmdHeader.channelID == channelID)
                {
                    peer.outCmds.Remove(currentCommand);
                    break;
                }
            }

            if (outgoingCommand == null)
                return CSENetProtoCmdType.None;

            wasSent = 0;
        }

        if (outgoingCommand == null)
            return CSENetProtoCmdType.None;

        if (channelID < peer.ChannelCount && peer.channels != null)
        {
            CSENetChannel channel = peer.channels[channelID];
            uint reliableWindow = reliableSequenceNumber / (uint)CSENetDef.PeerReliableWindowSize;
            if (channel.reliableWindows[reliableWindow] > 0)
            {
                --channel.reliableWindows[reliableWindow];
                if (channel.reliableWindows[reliableWindow] != 0)
                    channel.usedReliableWindows &= ~(1 << (int)reliableWindow);
            }
        }

        commandNumber = (CSENetProtoCmdType)(outgoingCommand.cmdHeader.cmdFlag & (int)CSENetProtoCmdType.Mask);

        if (outgoingCommand.packet != null)
        {
            if (wasSent != 0)
                peer.reliableDataInTransit -= outgoingCommand.fragmentLength;

            outgoingCommand.packet = null;
        }

        if (peer.sentReliableCmds.Count == 0)
            return commandNumber;

        outgoingCommand = peer.sentReliableCmds.First();

        if (outgoingCommand != null)
        {
            peer.nextTimeout = outgoingCommand.sentTime + outgoingCommand.rttTimeout;
        }

        return commandNumber;
    }

    public int ProtoReceiveIncomingCommands(CSENetEvent? @event)
    {
        if (this.packetData == null || this.packetData[0] == null)
            return -1;

        int packets;

        for (packets = 0; packets < 256; ++packets)
        {
            if (this.packetData == null || this.packetData[0] == null)
                return -1;

            int receivedLength = 0;

            if (this.socket == null)
                return -1;

            receivedLength = this.socket.Receive(this.packetData[0], ref this.receivedAddress);
            

            if (receivedLength < 0)
                return -1;

            if (receivedLength == 0)
                return 0;

            this.receivedData = this.packetData[0];
            this.receivedDataLength = receivedLength;

            this.totalReceivedData += receivedLength;
            this.totalReceivedPackets++;

            switch (ProtoHandleIncomingCommands(@event))
            {
                case 1:
                    return 1;

                case -1:
                    return -1;

                default:
                    break;
            }
        }

        return 0;
    }

    #region Proto Handle

    public int ProtoHandleIncomingCommands(CSENetEvent? @event)//TODO:delete
    {
        CSENetProtoHeader? header;
        CSENetProtoCmdHeader? commandHeader;
        CSENetPeer? peer = null;
        int currentDataIdx = 0;
        int headerSize = Marshal.SizeOf<CSENetProtoHeader>();
        uint peerID, flags;
        uint sessionID;

        if (this.receivedData == null)
        {
            return -1;
        }

        if (headerSize > this.receivedDataLength)
        {
            return 0;
        }

        byte[] headerBytes = CSENetUtils.SubBytes(this.receivedData, 0, headerSize);

        header = CSENetUtils.DeSerialize<CSENetProtoHeader>(headerBytes);

        peerID = CSENetUtils.NetToHostOrder(header.peerID);
        sessionID = (peerID & (int)CSENetProtoFlag.HeaderSessionMask) >> (int)CSENetProtoFlag.HeaderSessionShift;
        flags = peerID & (int)CSENetProtoFlag.HeaderFalgMASK;
        peerID &= ~((uint)CSENetProtoFlag.HeaderFalgMASK | (uint)CSENetProtoFlag.HeaderSessionMask);

        //TODO：这里最后得0，是原先(uint)&((ENetProtoHeader*)0)->sentTime;，不是很理解，先用0代替
        headerSize = (flags & (int)CSENetProtoFlag.HeaderFalgSentTime) != 0 ? (int)headerSize : 0;

        if (peerID == (int)CSENetDef.ProtoMaxPeerID)
            peer = null;
        else if (peerID >= this.peerCount)
            return 0;
        else
        {
            if (this.peers != null && peerID < this.peers.Length)
            {

                peer = this.peers[peerID];
                byte[]? hostIP = peer.address?.Address?.GetAddressBytes();
                bool isBroadcast = false;
                if (hostIP != null)
                {
                    isBroadcast = hostIP[0] != 255 && hostIP[1] != 255 && hostIP[2] != 255 && hostIP[3] != 255;
                }

                if (peer.state == CSENetPeerState.Disconnected ||
                    peer.state == CSENetPeerState.Zombie ||
                    ((this.receivedAddress?.Address?.GetAddressBytes() != peer.address?.Address?.GetAddressBytes() ||
                      this.receivedAddress?.Port != peer.address?.Port) &&
                      !isBroadcast) ||
                    (peer.outPeerID < (int)CSENetDef.ProtoMaxPeerID &&
                     sessionID != peer.inSessionID))
                    return 0;
            }
        }

        if (peer != null && peer.address != null && this.receivedAddress != null)
        {
            peer.address.Address = this.receivedAddress.Address;
            peer.address.Port = this.receivedAddress.Port;
            peer.inDataTotal += this.receivedDataLength;
        }

        currentDataIdx = headerSize;

        while (currentDataIdx < this.receivedDataLength)
        {
            int commandNumber;
            int commandSize;


            if (currentDataIdx + Marshal.SizeOf<CSENetProtoCmdHeader>() > this.receivedDataLength)//TODO:所有sizeof都用这种形式
                break;

            byte[] objBytes = CSENetUtils.SubBytes(this.receivedData, currentDataIdx, Marshal.SizeOf<CSENetProtoCmdHeader>());
            commandHeader = CSENetUtils.DeSerialize<CSENetProtoCmdHeader>(objBytes);

            commandNumber = commandHeader.cmdFlag & (int)CSENetProtoCmdType.Mask;
            if (commandNumber >= (int)CSENetProtoCmdType.Count)
                break;

            commandSize = Convert.ToInt32(CSENetProtoCmdSize.CmdSize[commandNumber]);
            if (commandSize == 0 || currentDataIdx + commandSize > this.receivedDataLength)
                break;

            int commandStartIdx = currentDataIdx;
            currentDataIdx += commandSize;

            if (peer == null && commandNumber != (int)CSENetProtoCmdType.Connect)
                break;

            commandHeader.reliableSeqNum = CSENetUtils.NetToHostOrder(commandHeader.reliableSeqNum);

            //TODO：switch-case

            if (peer != null &&
                (commandHeader.cmdFlag & (int)CSENetProtoFlag.CmdFlagAck) != 0)
            {
                uint sentTime;

                if ((flags & (int)CSENetProtoFlag.HeaderFalgSentTime) == 0)
                    break;

                sentTime = CSENetUtils.NetToHostOrder(header.sentTime);

                switch (peer.state)
                {
                    case CSENetPeerState.Disconnecting:
                    case CSENetPeerState.AckConnect:
                    case CSENetPeerState.Disconnected:
                    case CSENetPeerState.Zombie:
                        break;

                    case CSENetPeerState.AckDisconnect:
                        if ((commandHeader.cmdFlag & (int)CSENetProtoCmdType.Mask) == (int)CSENetProtoCmdType.Disconnect)
                            peer.QueueAck(commandHeader, sentTime);
                        break;

                    default:
                        peer.QueueAck(commandHeader, sentTime);
                        break;
                }
            }
        }

    commandError:
        if (@event != null && @event.type != CSENetEventType.None)
            return 1;

        return 0;
    }





    public void ProtoSendOutCmds(int? a, int b)//TODO:delete
    {

    }

    #endregion



    #endregion
}
