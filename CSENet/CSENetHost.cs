using System.Net;
using System.Net.Sockets;
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
    public bool IfContinueSending;
    public uint packetSize;
    public CSENetProtoFlag headerFlags;
    public uint SessionID = -1;
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

    public int HostService(CSENetEvent? evt, long timeout)
    {
        if (evt != null)
        {
            evt.type = CSENetEventType.None;
            evt.peer = null;
            evt.packet = null;

            switch (ProtoDispatchIncomingCommands(ref evt))
            {
                case 1:
                    return 1;
                case -1:
                    return -1;
                default:
                    break;
            }
        }

        this.serviceTime = CSENetUtils.TimeGet();

        timeout += this.serviceTime;//超时绝对时间
        bool waitSuccess = false;
        do
        {
            if (NeedCheckBandwidthThrottle())
                BandwidthThrottle();

            switch (ProtoSendOutCmds(evt, 1))
            {
                case 1:
                    return 1;
                case -1:
                    return -1;
                default:
                    break;
            }

            switch (ProtoReceiveIncomingCommands(evt))
            {
                case 1:
                    return 1;
                case -1:
                    return -1;
                default:
                    break;
            }

            switch (ProtoSendOutCmds(evt, 1))
            {
                case 1:
                    return 1;
                case -1:
                    return -1;
                default:
                    break;
            }

            if (evt != null)
            {
                switch (ProtoDispatchIncomingCommands(ref evt))
                {
                    case 1:
                        return 1;
                    case -1:
                        return -1;
                    default:
                        break;
                }
            }

            if (this.serviceTime >= timeout)
                return 0;

            do
            {
                this.serviceTime = CSENetUtils.TimeGet();

                if (this.serviceTime >= timeout)
                    return 0;

                if (this.socket == null)
                    return -1;
                waitSuccess = this.socket.Wait((int)Math.Abs(timeout - this.serviceTime), SelectMode.SelectRead);
            }
            while (!waitSuccess);

            this.serviceTime = CSENetUtils.TimeGet();
        } while (waitSuccess);

        return 0;
    }

    private bool NeedCheckBandwidthThrottle()
    {
        return Math.Abs(this.serviceTime - this.bandwidthThrottleEpoch) >= (uint)CSENetDef.HostBandwidthThrottleInterval;
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

    public void ProtoSendAcknowledgements(CSENetPeer peer)
    {
        if (peer.acknowledgements.Count == 0)
        {
            return;
        }

        CSENetAckCmd acknowledgement;
        CSENetAckCmd currentAcknowledgement;
        uint reliableSequenceNumber;

        currentAcknowledgement = peer.acknowledgements.First();

        while (peer.acknowledgements.Count > 0)
        {
            acknowledgement = peer.acknowledgements.First();

            CSENetProto command = new();

            if (this.CommandCount > CSENetDef.ProtoMaxPacketCmds ||
                this.BufferCount >= CSENetDef.BufferMax ||
                peer.mtu - this.packetSize < Marshal.SizeOf<CSENetProtoAck>())
            {
                this.IfContinueSending = 1;

                break;
            }

            reliableSequenceNumber = (uint)IPAddress.HostToNetworkOrder(acknowledgement.cmdHeader.reliableSeqNum);

            command.header.cmdFlag = (int)CSENetProtoCmdType.Ack;
            command.header.channelID = acknowledgement.cmdHeader.channelID;
            command.header.reliableSeqNum = reliableSequenceNumber;
            command.ack = new();
            command.ack.receivedReliableSeqNum = reliableSequenceNumber;
            command.ack.receivedSentTime = (uint)IPAddress.HostToNetworkOrder(acknowledgement.sentTime);

            if ((acknowledgement.cmdHeader.cmdFlag & (int)CSENetProtoCmdType.Mask) == (int)CSENetProtoCmdType.Disconnect)
                ProtoDispatchState(peer, CSENetPeerState.Zombie);

            peer.acknowledgements.RemoveAt(0);

            byte[]? buffer = CSENetUtils.Serialize<CSENetProtoAck>(command.ack);
            if (buffer == null)
            {
                continue;
            }

            this.packetSize += (uint)buffer.Length;

            this.commands.Add(command);
            this.buffers.Add(buffer);
        }

    }

    public int ProtoCheckTimeouts(CSENetPeer peer, CSENetEvent? @event)
    {
        CSENetOutCmd outgoingCommand;
        CSENetOutCmd currentCommand;

        int i = 0;
        while (i < peer.sentReliableCmds.Count)
        {
            currentCommand = peer.sentReliableCmds[i];
            outgoingCommand = currentCommand;

            i++;

            if (Math.Abs(this.serviceTime - outgoingCommand.sentTime) < outgoingCommand.rttTimeout)
                continue;

            if (peer.earliestTimeout == 0 ||
                outgoingCommand.sentTime < peer.earliestTimeout)
                peer.earliestTimeout = outgoingCommand.sentTime;

            if (peer.earliestTimeout != 0 &&
                  (Math.Abs(this.serviceTime - peer.earliestTimeout) >= peer.timeoutMaximum ||
                    (outgoingCommand.rttTimeout >= outgoingCommand.rttTimeoutLimit &&
                      Math.Abs(this.serviceTime - peer.earliestTimeout) >= peer.timeoutMinimum)))
            {
                ProtoNotifyDisconnect(peer, @event);

                return 1;
            }

            if (outgoingCommand.packet != null)
                peer.reliableDataInTransit -= outgoingCommand.fragmentLength;

            ++peer.packetsLost;

            outgoingCommand.rttTimeout *= 2;

            peer.outCmds.Insert(0, outgoingCommand);
            peer.sentReliableCmds.Remove(outgoingCommand);


            if (currentCommand != null && peer.sentReliableCmds.Count != 0 && currentCommand == peer.sentReliableCmds.First()
               )
            {
                outgoingCommand = currentCommand;

                peer.nextTimeout = outgoingCommand.sentTime + outgoingCommand.rttTimeout;
            }
        }

        return 0;
    }

    public int ProtoCheckOutgoingCommands(CSENetPeer peer)
    {
        CSENetOutCmd outgoingCommand;
        CSENetOutCmd currentCommand;
        CSENetChannel? channel = null;
        uint reliableWindow = 0;
        uint commandSize;
        int windowExceeded = 0, windowWrap = 0, canPing = 1;

        int i = 0;
        while (i < peer.outCmds.Count)
        {
            currentCommand = peer.outCmds[i];
            outgoingCommand = currentCommand;

            if ( !outgoingCommand.cmdHeader.ProtoFlag.HasFlag(CSENetProtoFlag.CmdFlagAck) )
            {
                channel = outgoingCommand.cmdHeader.channelID < peer.ChannelCount ? peer.channels?[outgoingCommand.cmdHeader.channelID] : null;
                reliableWindow = outgoingCommand.reliableSeqNum / (uint)CSENetDef.PeerReliableWindowSize;
                if (channel != null)
                {
                    if (windowWrap == 0 &&
                         outgoingCommand.sendAttempts < 1 &&
                         (outgoingCommand.reliableSeqNum % (uint)CSENetDef.PeerReliableWindowSize) == 0 &&
                            (channel.reliableWindows[(reliableWindow + (uint)CSENetDef.PeerReliableWindows - 1) % (uint)CSENetDef.PeerReliableWindows] >= (uint)CSENetDef.PeerReliableWindowSize ||
                                (channel.usedReliableWindows &
                                ((((1 << ((int)CSENetDef.PeerFreeReliableWindows + 2)) - 1) << (int)reliableWindow) |
                                        (((1 << ((int)CSENetDef.PeerFreeReliableWindows + 2)) - 1) >> ((int)CSENetDef.PeerReliableWindows - (int)reliableWindow))
                                )) != 0
                             )
                        )
                        windowWrap = 1;
                    if (windowWrap == 0)
                    {
                        i++;

                        continue;
                    }
                }

                if (outgoingCommand.packet != null)
                {
                    if (windowExceeded != 0)
                    {
                        uint windowSize = (peer.packetThrottle * peer.windowSize) / (uint)CSENetDef.PeerPacketThrottleScale;

                        if (peer.reliableDataInTransit + outgoingCommand.fragmentLength > Math.Max(windowSize, peer.mtu))
                            windowExceeded = 1;
                    }
                    if (windowExceeded != 0)
                    {
                        i++;
                        continue;
                    }
                }

                canPing = 0;
            }//if

            CSENetProto command = outgoingCommand.cmd;
            byte[]? buffer = CSENetUtils.Serialize<CSENetProto>(command);
            if (buffer == null) continue;

            commandSize = Convert.ToUInt32(buffer.Length);
            if (this.CommandCount > CSENetDef.ProtoMaxPacketCmds ||
                this.BufferCount >= CSENetDef.BufferMax ||
                peer.mtu - this.packetSize < commandSize ||
                (outgoingCommand.packet != null &&
                (uint)(peer.mtu - this.packetSize) < (uint)(commandSize + outgoingCommand.fragmentLength)))
            {
                this.IfContinueSending = true;

                break;
            }

            i++;

            if ( !outgoingCommand.cmdHeader.ProtoFlag.HasFlag(CSENetProtoFlag.CmdFlagAck) )
            {
                if (channel != null && outgoingCommand.sendAttempts < 1)
                {
                    channel.usedReliableWindows |= 1 << (int)reliableWindow;
                    ++channel.reliableWindows[reliableWindow];
                }

                ++outgoingCommand.sendAttempts;

                if (outgoingCommand.rttTimeout == 0)
                {
                    outgoingCommand.rttTimeout = peer.rtt + 4 * peer.rttVariance;
                    outgoingCommand.rttTimeoutLimit = peer.timeoutLimit * outgoingCommand.rttTimeout;
                }

                if (peer.sentReliableCmds.Count == 0)
                    peer.nextTimeout = this.serviceTime + outgoingCommand.rttTimeout;

                peer.outCmds.Remove(currentCommand);
                peer.sentReliableCmds.Add(currentCommand);

                outgoingCommand.sentTime = this.serviceTime;

                this.headerFlags |= CSENetProtoFlag.HeaderFalgSentTime;

                peer.reliableDataInTransit += outgoingCommand.fragmentLength;
            }
            else
            {
                if (outgoingCommand.packet != null && outgoingCommand.fragmentOffset == 0)
                {
                    peer.packetThrottleCounter += (uint)CSENetDef.PeerPacketThrottleCounter;
                    peer.packetThrottleCounter %= (uint)CSENetDef.PeerPacketThrottleScale;

                    if (peer.packetThrottleCounter > peer.packetThrottle)
                    {
                        uint reliableSequenceNumber = outgoingCommand.reliableSeqNum,
                                    unreliableSequenceNumber = outgoingCommand.unreliableSeqNum;
                        for (; ; )
                        {
                            if (currentCommand == null)
                                break;

                            outgoingCommand = currentCommand;
                            if (outgoingCommand.reliableSeqNum != reliableSequenceNumber ||
                                outgoingCommand.unreliableSeqNum != unreliableSequenceNumber)
                                break;

                            i++;
                        }

                        continue;
                    }
                }

                peer.outCmds.Remove(currentCommand);

                if (outgoingCommand.packet != null)
                    peer.sentUnreliableCmds.Add(currentCommand);
            }

            if (buffer == null) continue;

            this.packetSize += (uint)buffer.Length;

            this.commands.Add(command);
            this.buffers.Add(buffer);

            if (outgoingCommand.packet != null && outgoingCommand.packet.Data != null)
            {
                byte[] newBuffer = new byte[outgoingCommand.fragmentLength];
                Array.Copy(newBuffer, 0, outgoingCommand.packet.Data, outgoingCommand.fragmentOffset, newBuffer.Length);

                this.packetSize += outgoingCommand.fragmentLength;

                this.buffers.Add(newBuffer);//TODO:分片的发送逻辑，等待重构
            }

            ++peer.packetsSent;

        }//while

        if (peer.state == CSENetPeerState.DisconnectLater &&
            peer.outCmds.Count == 0 &&
            peer.sentReliableCmds.Count == 0 &&
            peer.sentUnreliableCmds.Count == 0)
            peer.Disconnect(peer.@eventData);

        return canPing;
    }

    public int ProtoSendOutCmds(CSENetEvent? @event, int checkForTimeouts)
    {
        int sentLength = 0;

        if (this.peers == null || this.peers.Length == 0)
            return -1;

        this.IfContinueSending = true;

        while (this.IfContinueSending)
        {
            this.IfContinueSending = false;
            foreach (CSENetPeer currentPeer in this.peers)
            {
                if (currentPeer.state == CSENetPeerState.Disconnected ||
                    currentPeer.state == CSENetPeerState.Zombie)
                    continue;

                this.headerFlags = 0;
                this.commands.Clear();
                this.buffers.Clear();

                if (currentPeer.acknowledgements.Count > 0)
                    ProtoSendAcknowledgements(currentPeer);

                if (checkForTimeouts != 0 &&
                    currentPeer.sentReliableCmds.Count > 0 &&
                    this.serviceTime >= currentPeer.nextTimeout &&
                    ProtoCheckTimeouts(currentPeer, @event) == 1)
                {
                    if (@event != null && @event.type != CSENetEventType.None)
                        return 1;
                    else
                        continue;
                }

                if ((currentPeer.outCmds.Count == 0 ||
                      ProtoCheckOutgoingCommands(currentPeer) != 0) &&
                    currentPeer.sentReliableCmds.Count == 0 &&
                    Math.Abs(this.serviceTime - currentPeer.lastReceiveTime) >= currentPeer.pingInterval )
                {
                    currentPeer.Ping();
                    ProtoCheckOutgoingCommands(currentPeer);
                }

                if (this.CommandCount == 0)
                    continue;

                if (currentPeer.packetLossEpoch == 0)
                    currentPeer.packetLossEpoch = this.serviceTime;
                else if (Math.Abs(this.serviceTime - currentPeer.packetLossEpoch) >= (uint)CSENetDef.PeerPacketLossInterval &&
                    currentPeer.packetsSent > 0)
                {
                    //超时，就增大packetLoss
                    uint packetLoss = currentPeer.packetsLost * (uint)CSENetDef.PeerPacketLossScale / currentPeer.packetsSent;

                    currentPeer.packetLossVariance = (currentPeer.packetLossVariance * 3 + Math.Abs(packetLoss - currentPeer.packetLoss)) / 4;
                    currentPeer.packetLoss = (currentPeer.packetLoss * 7 + packetLoss) / 8;

                    currentPeer.packetLossEpoch = this.serviceTime;
                    currentPeer.packetsSent = 0;
                    currentPeer.packetsLost = 0;
                }

                CSENetProtoHeader header = new();
                if ( this.headerFlags.HasFlag(CSENetProtoFlag.HeaderFalgSentTime) )
                {
                    header.sentTime = (uint)IPAddress.HostToNetworkOrder(this.serviceTime & 0xFFFF);
                }

                if (currentPeer.outPeerID < (int)CSENetDef.ProtoMaxPeerID)
                    this.SessionID = currentPeer.outSessionID;

                header.peerID = (uint)IPAddress.HostToNetworkOrder(currentPeer.outPeerID);

                byte[]? buffer = CSENetUtils.Serialize<CSENetProtoHeader>(header);
                if (buffer != null)
                {
                    this.buffers.Add(buffer);
                }

                currentPeer.lastSendTime = this.serviceTime;

                if (this.socket != null)
                    sentLength = this.socket.SendTo(currentPeer.address, this.buffers);

                ProtoRemoveSentUnreliableCommands(currentPeer);

                if (sentLength < 0)
                    return -1;

                this.totalSentData += (uint)sentLength;
                this.totalSentPackets++;
            }
        }

        return 0;
    }

    #endregion

    #region Proto Handle

    public int ProtoHandleIncomingCommands(CSENetEvent? @event)//TODO:delete
    {
        CSENetProtoHeader? header;
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
        if (header == null) return -1;

        peerID = CSENetUtils.NetToHostOrder(header.peerID);
        sessionID = (peerID & (int)CSENetProtoFlag.HeaderSessionMask) >> (int)CSENetProtoFlag.HeaderSessionShift;
        flags = peerID & (int)CSENetProtoFlag.HeaderFalgMASK;
        peerID &= ~((uint)CSENetProtoFlag.HeaderFalgMASK | (uint)CSENetProtoFlag.HeaderSessionMask);

        //TODO：这里最后的0，是原先(uint)&((ENetProtoHeader*)0)->sentTime;，不是很理解，先用0代替
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

            int commandSize = Convert.ToInt32(CSENetUtils.SubBytes(this.receivedData, currentDataIdx, 4));
            byte[] objBytes = CSENetUtils.SubBytes(this.receivedData, currentDataIdx+4, commandSize);
            CSENetProto? proto = CSENetUtils.DeSerialize<CSENetProto>(objBytes);
            if (proto == null) continue;

            commandNumber = proto.header.cmdFlag & (int)CSENetProtoCmdType.Mask;
            if (commandNumber >= (int)CSENetProtoCmdType.Count)
                break;

            int commandStartIdx = currentDataIdx;
            currentDataIdx += commandSize;

            if (peer == null && commandNumber != (int)CSENetProtoCmdType.Connect)
                break;

            proto.header.reliableSeqNum = CSENetUtils.NetToHostOrder(proto.header.reliableSeqNum);

            switch (commandNumber)
            {
                case (int)CSENetProtoCmdType.Ack:
                    if (peer == null || ProtoHandleAcknowledge(@event, peer, proto.header, commandStartIdx, commandSize) != 0)
                        goto commandError;
                    break;

                case (int)CSENetProtoCmdType.Connect:
                    if (peer != null)
                        goto commandError;
                    peer = ProtoHandleConnect(proto.header, commandStartIdx, commandSize);
                    if (peer == null)
                        goto commandError;
                    break;

                case (int)CSENetProtoCmdType.VerifyConnect:
                    if (peer == null || ProtoHandleVerifyConnect(@event, peer, commandStartIdx, commandSize) != 0)
                        goto commandError;
                    break;

                case (int)CSENetProtoCmdType.Disconnect:
                    if (peer == null || ProtoHandleDisconnect(proto.header, peer, commandStartIdx, commandSize) != 0)
                        goto commandError;
                    break;

                case (int)CSENetProtoCmdType.Ping:
                    if (peer == null || ProtoHandlePing(peer) != 0)
                        goto commandError;
                    break;

                case (int)CSENetProtoCmdType.SendReliable:
                    if (peer == null || ProtoHandleSendReliable(proto.header, peer, commandStartIdx, commandSize, ref currentDataIdx) != 0)
                        goto commandError;
                    break;

                case (int)CSENetProtoCmdType.SendFragment:
                    if (peer == null || ProtoHandleSendFragment(proto.header, peer, commandStartIdx, commandSize, ref currentDataIdx) != 0)
                        goto commandError;
                    break;

                case (int)CSENetProtoCmdType.SendUnreliable:
                    if (peer == null || ProtoHandleSendUnreliable(proto.header, peer, commandStartIdx, commandSize, ref currentDataIdx) != 0)
                        goto commandError;
                    break;

                case (int)CSENetProtoCmdType.SendUnreliableFragment:
                    if (peer == null || ProtoHandleSendUnreliableFragment(proto.header, peer, commandStartIdx, commandSize, ref currentDataIdx) != 0)
                        goto commandError;
                    break;

                case (int)CSENetProtoCmdType.SendUnseq:
                    if (peer == null || ProtoHandleSendUnsequenced(proto.header, peer, commandStartIdx, commandSize, ref currentDataIdx) != 0)
                        goto commandError;
                    break;

                case (int)CSENetProtoCmdType.BandwidthLimit:
                    if (peer == null || ProtoHandleBandwidthLimit(proto.header, peer, commandStartIdx, commandSize) != 0)
                        goto commandError;
                    break;

                case (int)CSENetProtoCmdType.ThrottleConfig:
                    if (peer == null || ProtoHandleThrottleConfigure(proto.header, peer, commandStartIdx, commandSize) != 0)
                        goto commandError;
                    break;

                default:
                    goto commandError;
            }

            if (peer != null &&
                (proto.header.cmdFlag & (int)CSENetProtoFlag.CmdFlagAck) != 0)
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
                        if ((proto.header.cmdFlag & (int)CSENetProtoCmdType.Mask) == (int)CSENetProtoCmdType.Disconnect)
                            peer.QueueAck(proto.header, sentTime);
                        break;

                    default:
                        peer.QueueAck(proto.header, sentTime);
                        break;
                }
            }
        }

    commandError:
        if (@event != null && @event.type != CSENetEventType.None)
            return 1;

        return 0;
    }

    public int ProtoHandleAcknowledge(CSENetEvent? @event, CSENetPeer peer, CSENetProtoCmdHeader commandHeader, int commandStartIdx, int commandSize)
    {
        long roundTripTime,
               receivedSentTime;
        uint receivedReliableSequenceNumber;
        CSENetProtoCmdType commandNumber;

        if (this.receivedData == null) return 0;

        CSENetProtoAck? ackCmd = CSENetUtils.DeSerialize<CSENetProtoAck>(CSENetUtils.SubBytes(this.receivedData, commandStartIdx, commandSize));
        if (ackCmd == null) return 0;

        if (peer.state == CSENetPeerState.Disconnected || peer.state == CSENetPeerState.Zombie)
            return 0;

        receivedSentTime = CSENetUtils.NetToHostOrder(ackCmd.receivedSentTime);
        receivedSentTime |= this.serviceTime & 0xFFFF0000;
        if ((receivedSentTime & 0x8000) > (this.serviceTime & 0x8000))
            receivedSentTime -= 0x10000;

        if (this.serviceTime < receivedSentTime)
            return 0;

        roundTripTime = Math.Abs(this.serviceTime - receivedSentTime);
        roundTripTime = Math.Max(roundTripTime, 1);

        if (peer.lastReceiveTime > 0)
        {
            peer.Throttle(roundTripTime);

            peer.rttVariance -= peer.rttVariance / 4;

            if (roundTripTime >= peer.rtt)
            {
                long diff = roundTripTime - peer.rtt;
                peer.rttVariance += diff / 4;
                peer.rtt += diff / 8;
            }
            else
            {
                long diff = peer.rtt - roundTripTime;
                peer.rttVariance += diff / 4;
                peer.rtt -= diff / 8;
            }
        }
        else
        {
            peer.rtt = roundTripTime;
            peer.rttVariance = (roundTripTime + 1) / 2;
        }

        if (peer.rtt < peer.lowestRoundTripTime)
            peer.lowestRoundTripTime = peer.rtt;

        if (peer.rttVariance > peer.highestRoundTripTimeVariance)
            peer.highestRoundTripTimeVariance = peer.rttVariance;

        if (peer.packetThrottleEpoch == 0 ||
            Math.Abs(this.serviceTime - peer.packetThrottleEpoch) >= peer.packetThrottleInterval)
        {
            peer.lastRoundTripTime = peer.lowestRoundTripTime;
            peer.lastRTTVariance = Math.Max(peer.highestRoundTripTimeVariance, 1);
            peer.lowestRoundTripTime = peer.rtt;
            peer.highestRoundTripTimeVariance = peer.rttVariance;
            peer.packetThrottleEpoch = this.serviceTime;
        }

        peer.lastReceiveTime = Math.Max(this.serviceTime, 1);
        peer.earliestTimeout = 0;

        receivedReliableSequenceNumber = CSENetUtils.NetToHostOrder(ackCmd.receivedReliableSeqNum);

        commandNumber = ProtoRemoveSentReliableCommand(peer, receivedReliableSequenceNumber, commandHeader.channelID);

        switch (peer.state)
        {
            case CSENetPeerState.AckConnect:
                if (commandNumber != CSENetProtoCmdType.VerifyConnect)
                    return -1;

                ProtoNotifyConnect(peer, @event);
                break;

            case CSENetPeerState.Disconnecting:
                if (commandNumber != CSENetProtoCmdType.Disconnect)
                    return -1;

                ProtoNotifyDisconnect(peer, @event);
                break;

            case CSENetPeerState.DisconnectLater:
                if (peer.outCmds.Count == 0 &&
                    peer.sentReliableCmds.Count == 0)
                    peer.Disconnect(peer.@eventData);
                break;

            default:
                break;
        }

        return 0;
    }

    public CSENetPeer? ProtoHandleConnect(CSENetProtoCmdHeader commandHeader, int commandStartIdx, int commandSize)
    {
        uint incomingSessionID, outgoingSessionID;
        uint mtu, windowSize;
        uint channelCount, duplicatePeers = 0;
        CSENetPeer? peer = null;

        if (this.receivedData == null) return null;
        CSENetProtoConnect? connectCmd = CSENetUtils.DeSerialize<CSENetProtoConnect>(CSENetUtils.SubBytes(this.receivedData, commandStartIdx, commandSize));
        if (connectCmd == null) return null;

        channelCount = CSENetUtils.NetToHostOrder(connectCmd.channelCount);

        if (channelCount < (int)CSENetDef.ProtoMinChannelCount ||
            channelCount > (int)CSENetDef.ProtoMaxChannelCount)
            return null;

        if (this.peers == null)
        {
            return null;
        }

        foreach (var currentPeer in this.peers)
        {
            if (currentPeer.state == (int)CSENetPeerState.Disconnected)
            {
                if (peer == null)
                    peer = currentPeer;
            }
            else
            if (currentPeer.state != CSENetPeerState.Connecting &&
                currentPeer.address?.Address.GetAddressBytes() == this.receivedAddress?.Address.GetAddressBytes())
            {
                if (currentPeer?.address?.Port == this.receivedAddress?.Port &&
                    currentPeer?.connectID == connectCmd.connectID)
                    return null;

                ++duplicatePeers;
            }
        }

        if (peer == null || duplicatePeers >= this.duplicatePeers)
            return null;

        if (channelCount > this.channelLimit)
            channelCount = this.channelLimit;

        peer.channels = new CSENetChannel[channelCount];
        for (int i = 0; i < channelCount; i++)
        {
            peer.channels[i] = new CSENetChannel();
        }

        peer.state = CSENetPeerState.AckConnect;
        peer.connectID = connectCmd.connectID;
        peer.address = this.receivedAddress;
        peer.outPeerID = CSENetUtils.NetToHostOrder(connectCmd.outPeerID);
        peer.inBandwidth = CSENetUtils.NetToHostOrder(connectCmd.inBandwidth);
        peer.outBandwidth = CSENetUtils.NetToHostOrder(connectCmd.outBandwidth);
        peer.packetThrottleInterval = CSENetUtils.NetToHostOrder(connectCmd.packetThrottleInterval);
        peer.packetThrottleAcceleration = CSENetUtils.NetToHostOrder(connectCmd.packetThrottleAcceleration);
        peer.packetThrottleDeceleration = CSENetUtils.NetToHostOrder(connectCmd.packetThrottleDeceleration);
        peer.@eventData = CSENetUtils.NetToHostOrder(connectCmd.data);

        incomingSessionID = connectCmd.inSessionID == 0xFF ? peer.outSessionID : connectCmd.inSessionID;
        incomingSessionID = (incomingSessionID + 1) & ((int)CSENetProtoFlag.HeaderSessionMask >> (int)CSENetProtoFlag.HeaderSessionShift);
        if (incomingSessionID == peer.outSessionID)
            incomingSessionID = (incomingSessionID + 1) & ((int)CSENetProtoFlag.HeaderSessionMask >> (int)CSENetProtoFlag.HeaderSessionShift);
        peer.outSessionID = incomingSessionID;

        outgoingSessionID = connectCmd.outSessionID == 0xFF ? peer.inSessionID : connectCmd.outSessionID;
        outgoingSessionID = (outgoingSessionID + 1) & ((int)CSENetProtoFlag.HeaderSessionMask >> (int)CSENetProtoFlag.HeaderSessionShift);
        if (outgoingSessionID == peer.inSessionID)
            outgoingSessionID = (outgoingSessionID + 1) & ((int)CSENetProtoFlag.HeaderSessionMask >> (int)CSENetProtoFlag.HeaderSessionShift);
        peer.inSessionID = outgoingSessionID;

        mtu = CSENetUtils.NetToHostOrder(connectCmd.mtu);

        if (mtu < (int)CSENetDef.ProtoMinMTU)
            mtu = (int)CSENetDef.ProtoMinMTU;
        else
        if (mtu > (int)CSENetDef.ProtoMaxMTU)
            mtu = (int)CSENetDef.ProtoMaxMTU;

        peer.mtu = mtu;

        if (this.outBandwidth == 0 &&
            peer.inBandwidth == 0)
            peer.windowSize = (int)CSENetDef.ProtoMaxWindowSize;
        else
        if (this.outBandwidth == 0 ||
            peer.inBandwidth == 0)
            peer.windowSize = (Math.Max(this.outBandwidth, peer.inBandwidth) /
                                          (uint)CSENetDef.PeerWindowSizeScale) *
                                            (int)CSENetDef.ProtoMinWindowSize;
        else
            peer.windowSize = (Math.Min(this.outBandwidth, peer.inBandwidth) /
                                          (uint)CSENetDef.PeerWindowSizeScale) *
                                            (int)CSENetDef.ProtoMinWindowSize;

        if (peer.windowSize < (int)CSENetDef.ProtoMinWindowSize)
            peer.windowSize = (int)CSENetDef.ProtoMinWindowSize;
        else
        if (peer.windowSize > (int)CSENetDef.ProtoMaxWindowSize)
            peer.windowSize = (int)CSENetDef.ProtoMaxWindowSize;


        if (this.inBandwidth == 0)
            windowSize = (int)CSENetDef.ProtoMaxWindowSize;
        else
            windowSize = (this.inBandwidth / (uint)CSENetDef.PeerWindowSizeScale) *
                           (int)CSENetDef.ProtoMinWindowSize;

        if (windowSize > CSENetUtils.NetToHostOrder(connectCmd.windowSize))
            windowSize = CSENetUtils.NetToHostOrder(connectCmd.windowSize);

        if (windowSize < (int)CSENetDef.ProtoMinWindowSize)
            windowSize = (int)CSENetDef.ProtoMinWindowSize;
        else
        if (windowSize > (int)CSENetDef.ProtoMaxWindowSize)
            windowSize = (int)CSENetDef.ProtoMaxWindowSize;


        CSENetProto verifyCommand = new();
        verifyCommand.header.cmdFlag = (int)CSENetProtoCmdType.VerifyConnect | (int)CSENetProtoFlag.CmdFlagAck;
        verifyCommand.header.channelID = 0xFF;
        verifyCommand.verifyConnect = new();
        verifyCommand.verifyConnect.outPeerID = (uint)IPAddress.HostToNetworkOrder(peer.inPeerID);
        verifyCommand.verifyConnect.inSessionID = incomingSessionID;
        verifyCommand.verifyConnect.outSessionID = outgoingSessionID;
        verifyCommand.verifyConnect.mtu = (uint)IPAddress.HostToNetworkOrder(peer.mtu);
        verifyCommand.verifyConnect.windowSize = (uint)IPAddress.HostToNetworkOrder(windowSize);
        verifyCommand.verifyConnect.channelCount = (uint)IPAddress.HostToNetworkOrder(channelCount);
        verifyCommand.verifyConnect.inBandwidth = (uint)IPAddress.HostToNetworkOrder(this.inBandwidth);
        verifyCommand.verifyConnect.outBandwidth = (uint)IPAddress.HostToNetworkOrder(this.outBandwidth);
        verifyCommand.verifyConnect.packetThrottleInterval = (uint)IPAddress.HostToNetworkOrder(peer.packetThrottleInterval);
        verifyCommand.verifyConnect.packetThrottleAcceleration = (uint)IPAddress.HostToNetworkOrder(peer.packetThrottleAcceleration);
        verifyCommand.verifyConnect.packetThrottleDeceleration = (uint)IPAddress.HostToNetworkOrder(peer.packetThrottleDeceleration);
        verifyCommand.verifyConnect.connectID = peer.connectID;

        peer.QueueOutgoingCommand(verifyCommand, null, 0, 0);

        return peer;
    }

    public int ProtoHandleVerifyConnect(CSENetEvent? @event, CSENetPeer peer, int commandStartIdx, int commandSize)
    {
        if (this.receivedData == null) return -1;
        CSENetProtoVerifyConnect? verifyConnectCmd = CSENetUtils.DeSerialize<CSENetProtoVerifyConnect>(CSENetUtils.SubBytes(this.receivedData, commandStartIdx, commandSize));
        if (verifyConnectCmd == null) return -1;

        uint mtu, windowSize;
        uint channelCount;

        if (peer.state != CSENetPeerState.Connecting)
            return 0;

        channelCount = CSENetUtils.NetToHostOrder(verifyConnectCmd.channelCount);

        if (channelCount < (int)CSENetDef.ProtoMinChannelCount || channelCount > (int)CSENetDef.ProtoMaxChannelCount ||
            CSENetUtils.NetToHostOrder(verifyConnectCmd.packetThrottleInterval) != peer.packetThrottleInterval ||
         CSENetUtils.NetToHostOrder(verifyConnectCmd.packetThrottleAcceleration) != peer.packetThrottleAcceleration ||
         CSENetUtils.NetToHostOrder(verifyConnectCmd.packetThrottleDeceleration) != peer.packetThrottleDeceleration ||
         verifyConnectCmd.connectID != peer.connectID)
        {
            peer.@eventData = 0;
            ProtoDispatchState(peer, CSENetPeerState.Zombie);
            return -1;
        }

        ProtoRemoveSentReliableCommand(peer, 1, 0xFF);

        peer.outPeerID = CSENetUtils.NetToHostOrder(verifyConnectCmd.outPeerID);
        peer.inSessionID = verifyConnectCmd.inSessionID;
        peer.outSessionID = verifyConnectCmd.outSessionID;

        mtu = CSENetUtils.NetToHostOrder(verifyConnectCmd.mtu);

        if (mtu < (int)CSENetDef.ProtoMinMTU)
            mtu = (int)CSENetDef.ProtoMinMTU;
        else
        if (mtu > (int)CSENetDef.ProtoMaxMTU)
            mtu = (int)CSENetDef.ProtoMaxMTU;

        if (mtu < peer.mtu)
            peer.mtu = mtu;

        windowSize = CSENetUtils.NetToHostOrder(verifyConnectCmd.windowSize);

        if (windowSize < (int)CSENetDef.ProtoMinWindowSize)
            windowSize = (int)CSENetDef.ProtoMinWindowSize;

        if (windowSize > (int)CSENetDef.ProtoMaxWindowSize)
            windowSize = (int)CSENetDef.ProtoMaxWindowSize;

        if (windowSize < peer.windowSize)
            peer.windowSize = windowSize;

        peer.inBandwidth = CSENetUtils.NetToHostOrder(verifyConnectCmd.inBandwidth);
        peer.outBandwidth = CSENetUtils.NetToHostOrder(verifyConnectCmd.outBandwidth);

        ProtoNotifyConnect(peer, @event);
        return 0;
    }

    public int ProtoHandleDisconnect(CSENetProtoCmdHeader commandHeader, CSENetPeer peer, int commandStartIdx, int commandSize)
    {
        if (peer.state == (int)CSENetPeerState.Disconnected || peer.state == CSENetPeerState.Zombie || peer.state == CSENetPeerState.AckDisconnect)
            return 0;

        if (this.receivedData == null) return -1;
        CSENetProtoDisconnect? disconnectCmd = CSENetUtils.DeSerialize<CSENetProtoDisconnect>(CSENetUtils.SubBytes(this.receivedData, commandStartIdx, commandSize));
        if (disconnectCmd == null) return -1;

        peer.ResetQueues();

        if (peer.state == CSENetPeerState.ConnectionSucceed || peer.state == CSENetPeerState.Disconnecting || peer.state == CSENetPeerState.Connecting)
            ProtoDispatchState(peer, CSENetPeerState.Zombie);
        else
        if (peer.state != CSENetPeerState.Connected && peer.state != CSENetPeerState.DisconnectLater)
        {
            if (peer.state == CSENetPeerState.ConnectionPending) this.recalculateBandwidthLimits = 1;

            peer.Reset();
        }
        else
        if ((commandHeader.cmdFlag & (int)CSENetProtoFlag.CmdFlagAck) != 0)
            ProtoChangeState(peer, CSENetPeerState.AckDisconnect);
        else
            ProtoDispatchState(peer, CSENetPeerState.Zombie);

        if (peer.state != (int)CSENetPeerState.Disconnected)
        {
            peer.@eventData = CSENetUtils.NetToHostOrder(disconnectCmd.data);
        }

        return 0;
    }

    public int ProtoHandlePing(CSENetPeer peer)
    {
        if (peer.state != CSENetPeerState.Connected && peer.state != CSENetPeerState.DisconnectLater)
            return -1;

        return 0;
    }

    public int ProtoHandleSendUnreliable(CSENetProtoCmdHeader commandHeader, CSENetPeer peer, int commandStartIdx, int commandSize, ref int currentDataIdx)
    {
        uint dataLength;

        if (commandHeader.channelID >= peer.ChannelCount ||
            (peer.state != CSENetPeerState.Connected && peer.state != CSENetPeerState.DisconnectLater))
            return -1;

        if (this.receivedData == null) return -1;
        CSENetProtoSendUnReliable? sendUnReliableCmd = CSENetUtils.DeSerialize<CSENetProtoSendUnReliable>(CSENetUtils.SubBytes(this.receivedData, commandStartIdx, commandSize));
        if (sendUnReliableCmd == null) return -1;

        dataLength = CSENetUtils.NetToHostOrder(sendUnReliableCmd.dataLength);
        byte[] packetData = CSENetUtils.SubBytes(this.receivedData, currentDataIdx, (int)dataLength);
        currentDataIdx += (int)dataLength;

        if (dataLength > this.maximumPacketSize ||
            currentDataIdx > this.receivedDataLength)
            return -1;

        if (peer.QueueInCmd(commandHeader, packetData, dataLength, 0, 0, sendUnReliableCmd.unreliableSeqNum) == null)
            return -1;

        return 0;
    }

    public int ProtoHandleSendReliable(CSENetProtoCmdHeader commandHeader, CSENetPeer peer, int commandStartIdx, int commandSize, ref int currentDataIdx)
    {
        if (this.receivedData == null) return -1;
        CSENetProtoSendReliable? sendReliableCmd = CSENetUtils.DeSerialize<CSENetProtoSendReliable>(CSENetUtils.SubBytes(this.receivedData, commandStartIdx, commandSize));
        if (sendReliableCmd == null) return -1;

        uint dataLength;

        if (commandHeader.channelID >= peer.ChannelCount ||
            (peer.state != CSENetPeerState.Connected && peer.state != CSENetPeerState.DisconnectLater))
            return -1;

        dataLength = CSENetUtils.NetToHostOrder(sendReliableCmd.dataLength);
        if (dataLength > this.maximumPacketSize)
            return -1;

        byte[] packetData = CSENetUtils.SubBytes(this.receivedData, currentDataIdx, (int)dataLength);
        currentDataIdx += (int)dataLength;

        if (this.receivedData.Length <= currentDataIdx)
            return -1;

        if (peer.QueueInCmd(commandHeader, packetData, dataLength, (int)CSENetPacketFlag.Reliable, 0) == null)
            return -1;

        return 0;
    }

    public int ProtoHandleBandwidthLimit(CSENetProtoCmdHeader commandHeader, CSENetPeer peer, int commandStartIdx, int commandSize)
    {
        if (peer.state != CSENetPeerState.Connected && peer.state != CSENetPeerState.DisconnectLater)
            return -1;

        if (peer.inBandwidth != 0)
            --this.bandwidthLimitedPeers;

        if (this.receivedData == null) return -1;
        CSENetProtoBandwidthLimit? bandwidthLimitCmd = CSENetUtils.DeSerialize<CSENetProtoBandwidthLimit>(CSENetUtils.SubBytes(this.receivedData, commandStartIdx, commandSize));
        if (bandwidthLimitCmd == null) return -1;

        peer.inBandwidth = CSENetUtils.NetToHostOrder(bandwidthLimitCmd.inBandwidth);
        peer.outBandwidth = CSENetUtils.NetToHostOrder(bandwidthLimitCmd.outBandwidth);

        if (peer.inBandwidth != 0)
            ++this.bandwidthLimitedPeers;

        if (peer.inBandwidth == 0 && this.outBandwidth == 0)
            peer.windowSize = (int)CSENetDef.ProtoMaxWindowSize;
        else
        if (peer.inBandwidth == 0 || this.outBandwidth == 0)
            peer.windowSize = (Math.Max(peer.inBandwidth, this.outBandwidth) /
                                   (uint)CSENetDef.PeerWindowSizeScale) * (int)CSENetDef.ProtoMinWindowSize;
        else
            peer.windowSize = (Math.Min(peer.inBandwidth, this.outBandwidth) /
                                   (uint)CSENetDef.PeerWindowSizeScale) * (int)CSENetDef.ProtoMinWindowSize;

        if (peer.windowSize < (int)CSENetDef.ProtoMinWindowSize)
            peer.windowSize = (int)CSENetDef.ProtoMinWindowSize;
        else
        if (peer.windowSize > (int)CSENetDef.ProtoMaxWindowSize)
            peer.windowSize = (int)CSENetDef.ProtoMaxWindowSize;

        return 0;
    }

    public int ProtoHandleSendFragment(CSENetProtoCmdHeader commandHeader, CSENetPeer peer, int commandStartIdx, int commandSize, ref int currentDataIdx)
    {
        uint fragmentNumber,
               fragmentCount,
               fragmentOffset,
               fragmentLength,
               startSequenceNumber,
               totalLength;
        CSENetChannel channel;
        uint startWindow, currentWindow;

        if (commandHeader.channelID >= peer.ChannelCount ||
            (peer.state != CSENetPeerState.Connected && peer.state != CSENetPeerState.DisconnectLater))
            return -1;

        if (this.receivedData == null) return -1;
        CSENetProtoSendFragment? sendFragmentCmd = CSENetUtils.DeSerialize<CSENetProtoSendFragment>(CSENetUtils.SubBytes(this.receivedData, commandStartIdx, commandSize));
        if (sendFragmentCmd == null) return -1;

        fragmentLength = CSENetUtils.NetToHostOrder(sendFragmentCmd.dataLength);
        currentDataIdx += (int)fragmentLength;
        if (fragmentLength > this.maximumPacketSize ||
            currentDataIdx > this.receivedDataLength)
            return -1;

        if (peer.channels == null) return -1;
        channel = peer.channels[commandHeader.channelID];
        startSequenceNumber = CSENetUtils.NetToHostOrder(sendFragmentCmd.startSeqNum);
        startWindow = startSequenceNumber / (uint)CSENetDef.PeerReliableWindowSize;
        currentWindow = channel.inReliableSeqNum / (uint)CSENetDef.PeerReliableWindowSize;

        if (startSequenceNumber < channel.inReliableSeqNum)
            startWindow += (uint)CSENetDef.PeerReliableWindows;

        if (startWindow < currentWindow || startWindow >= currentWindow + (uint)CSENetDef.PeerFreeReliableWindows - 1)
            return 0;

        fragmentNumber = CSENetUtils.NetToHostOrder(sendFragmentCmd.fragmentNum);
        fragmentCount = CSENetUtils.NetToHostOrder(sendFragmentCmd.fragmentCount);
        fragmentOffset = CSENetUtils.NetToHostOrder(sendFragmentCmd.fragmentOffset);
        totalLength = CSENetUtils.NetToHostOrder(sendFragmentCmd.totalLength);

        if (fragmentCount > (int)CSENetDef.ProtoMaxFragmentCount ||
            fragmentNumber >= fragmentCount ||
            totalLength > this.maximumPacketSize ||
            fragmentOffset >= totalLength ||
            fragmentLength > totalLength - fragmentOffset)
            return -1;

        CSENetInCmd currentCommand;
        CSENetInCmd? startCommand = null;
        for (int i = channel.inReliableCmds.Count - 1; i >= 0; i--)
        {
            currentCommand = channel.inReliableCmds[i];
            CSENetInCmd incomingcommand = currentCommand;

            if (startSequenceNumber >= channel.inReliableSeqNum)
            {
                if (incomingcommand.reliableSeqNum < channel.inReliableSeqNum)
                    continue;
            }
            else
            if (incomingcommand.reliableSeqNum >= channel.inReliableSeqNum)
                break;

            if (incomingcommand.reliableSeqNum <= startSequenceNumber)
            {
                if (incomingcommand.reliableSeqNum < startSequenceNumber)
                    break;

                if ((incomingcommand.cmdHeader.cmdFlag & (int)CSENetProtoCmdType.Mask) != (int)CSENetProtoCmdType.SendFragment ||
                    (incomingcommand.packet != null && totalLength != incomingcommand.packet.DataLength) ||
                     fragmentCount != incomingcommand.fragmentCount)
                    return -1;

                startCommand = incomingcommand;
                break;
            }
        }

        if (startCommand == null)
        {
            commandHeader.reliableSeqNum = startSequenceNumber;

            startCommand = peer.QueueInCmd(commandHeader, null, totalLength, (int)CSENetPacketFlag.Reliable, fragmentCount);
            if (startCommand == null)
                return -1;
        }

        if (startCommand.fragments != null && (startCommand.fragments[fragmentNumber / 32] & (uint)(1 << ((int)fragmentNumber % 32))) == 0)
        {
            --startCommand.fragmentsRemaining;

            startCommand.fragments[fragmentNumber / 32] |= (uint)(1 << ((int)fragmentNumber % 32));

            if (startCommand.packet != null && fragmentOffset + fragmentLength > startCommand.packet.DataLength)
                fragmentLength = startCommand.packet.DataLength - fragmentOffset;

            if (startCommand.packet != null && startCommand.packet.Data != null)
            {
                Array.Copy(startCommand.packet.Data, fragmentOffset, this.receivedData, currentDataIdx, fragmentLength);
            }

            if (startCommand.fragmentsRemaining <= 0)
                peer.DispatchInReliableCmds(channel, null);
        }

        return 0;
    }

    public int ProtoHandleThrottleConfigure(CSENetProtoCmdHeader commandHeader, CSENetPeer peer, int commandStartIdx, int commandSize)
    {
        if (peer.state != CSENetPeerState.Connected && peer.state != CSENetPeerState.DisconnectLater)
            return -1;

        if (this.receivedData == null) return -1;
        CSENetProtoThrottleConfigure? throttleConfigCmd = CSENetUtils.DeSerialize<CSENetProtoThrottleConfigure>(CSENetUtils.SubBytes(this.receivedData, commandStartIdx, commandSize));
        if (throttleConfigCmd == null) return -1;

        peer.packetThrottleInterval = CSENetUtils.NetToHostOrder(throttleConfigCmd.packetThrottleInterval);
        peer.packetThrottleAcceleration = CSENetUtils.NetToHostOrder(throttleConfigCmd.packetThrottleAcceleration);
        peer.packetThrottleDeceleration = CSENetUtils.NetToHostOrder(throttleConfigCmd.packetThrottleDeceleration);

        return 0;
    }

    public int ProtoHandleSendUnsequenced(CSENetProtoCmdHeader commandHeader, CSENetPeer peer, int commandStartIdx, int commandSize, ref int currentDataIdx)
    {
        uint unsequencedGroup, index;
        uint dataLength;

        if (commandHeader.channelID >= peer.ChannelCount ||
            (peer.state != CSENetPeerState.Connected && peer.state != CSENetPeerState.DisconnectLater))
            return -1;

        if (this.receivedData == null) return -1;
        CSENetProtoSendUnsequenced? sendUnseq = CSENetUtils.DeSerialize<CSENetProtoSendUnsequenced>(CSENetUtils.SubBytes(this.receivedData, commandStartIdx, commandSize));
        if (sendUnseq == null) return -1;

        dataLength = CSENetUtils.NetToHostOrder(sendUnseq.dataLength);
        currentDataIdx += (int)dataLength;
        if (dataLength > this.maximumPacketSize ||
             currentDataIdx > this.receivedDataLength)
            return -1;

        unsequencedGroup = CSENetUtils.NetToHostOrder(sendUnseq.unseqGroup);
        index = unsequencedGroup % (uint)CSENetDef.PeerUnseqWindowSize;

        if (unsequencedGroup < peer.inUnseqGroup)
            unsequencedGroup += 0x10000;

        if (unsequencedGroup >= (uint)peer.inUnseqGroup + (uint)CSENetDef.PeerUnseqWindows * (uint)CSENetDef.PeerUnseqWindowSize)
            return 0;

        unsequencedGroup &= 0xFFFF;

        if (unsequencedGroup - index != peer.inUnseqGroup)
        {
            peer.inUnseqGroup = unsequencedGroup - index;
            Array.Clear(peer.unseqWindow);
        }
        else
        if ((peer.unseqWindow[index / 32] & (uint)(1 << (int)(index % 32))) != 0)
            return 0;


        byte[] packetData = CSENetUtils.SubBytes(this.receivedData, currentDataIdx, (int)dataLength);
        currentDataIdx += (int)dataLength;

        if (peer.QueueInCmd(commandHeader, packetData, dataLength, (int)CSENetPacketFlag.UnSeq, 0) == null)
            return -1;

        peer.unseqWindow[index / 32] |= (uint)(1 << ((int)index % 32));

        return 0;
    }

    public int ProtoHandleSendUnreliableFragment(CSENetProtoCmdHeader commandHeader, CSENetPeer peer, int commandStartIdx, int commandSize, ref int currentDataIdx)
    {
        uint fragmentNumber,
               fragmentCount,
               fragmentOffset,
               fragmentLength,
               reliableSequenceNumber,
               startSequenceNumber,
               totalLength;
        uint reliableWindow, currentWindow;
        CSENetChannel channel;

        if (commandHeader.channelID >= peer.ChannelCount ||
            (peer.state != CSENetPeerState.Connected && peer.state != CSENetPeerState.DisconnectLater))
            return -1;

        if (this.receivedData == null) return -1;
        CSENetProtoSendFragment? sendFragmentCmd = CSENetUtils.DeSerialize<CSENetProtoSendFragment>(CSENetUtils.SubBytes(this.receivedData, commandStartIdx, commandSize));
        if (sendFragmentCmd == null) return -1;

        fragmentLength = CSENetUtils.NetToHostOrder(sendFragmentCmd.dataLength);
        currentDataIdx += (int)fragmentLength;
        if (fragmentLength > this.maximumPacketSize ||
            currentDataIdx > this.receivedDataLength)
            return -1;

        if (peer.channels == null) return -1;
        channel = peer.channels[commandHeader.channelID];

        reliableSequenceNumber = commandHeader.reliableSeqNum;
        startSequenceNumber = CSENetUtils.NetToHostOrder(sendFragmentCmd.startSeqNum);

        reliableWindow = reliableSequenceNumber / (uint)CSENetDef.PeerReliableWindowSize;
        currentWindow = channel.inReliableSeqNum / (uint)CSENetDef.PeerReliableWindowSize;

        if (reliableSequenceNumber < channel.inReliableSeqNum)
            reliableWindow += (uint)CSENetDef.PeerReliableWindows;

        if (reliableWindow < currentWindow || reliableWindow >= currentWindow + (uint)CSENetDef.PeerFreeReliableWindows - 1)
            return 0;

        if (reliableSequenceNumber == channel.inReliableSeqNum &&
            startSequenceNumber <= channel.inUnreliableSeqNum)
            return 0;

        fragmentNumber = CSENetUtils.NetToHostOrder(sendFragmentCmd.fragmentNum);
        fragmentCount = CSENetUtils.NetToHostOrder(sendFragmentCmd.fragmentCount);
        fragmentOffset = CSENetUtils.NetToHostOrder(sendFragmentCmd.fragmentOffset);
        totalLength = CSENetUtils.NetToHostOrder(sendFragmentCmd.totalLength);

        if (fragmentCount > (int)CSENetDef.ProtoMaxFragmentCount ||
            fragmentNumber >= fragmentCount ||
            totalLength > this.maximumPacketSize ||
            fragmentOffset >= totalLength ||
            fragmentLength > totalLength - fragmentOffset)
            return -1;

        CSENetInCmd currentCommand;
        CSENetInCmd? startCommand = null;

        for (int i = channel.inUnreliableCmds.Count - 1; i >= 0; i--)
        {
            currentCommand = channel.inUnreliableCmds[i];
            CSENetInCmd inCmd = currentCommand;

            if (reliableSequenceNumber >= channel.inReliableSeqNum)
            {
                if (inCmd.reliableSeqNum < channel.inReliableSeqNum)
                    continue;
            }
            else
            if (inCmd.reliableSeqNum >= channel.inReliableSeqNum)
                break;

            if (inCmd.reliableSeqNum < reliableSequenceNumber)
                break;

            if (inCmd.reliableSeqNum > reliableSequenceNumber)
                continue;

            if (inCmd.unreliableSeqNum <= startSequenceNumber)
            {
                if (inCmd.unreliableSeqNum < startSequenceNumber)
                    break;

                if ((inCmd.cmdHeader.cmdFlag & (int)CSENetProtoCmdType.Mask) != (int)CSENetProtoCmdType.SendUnreliableFragment ||
                    (inCmd.packet != null && totalLength != inCmd.packet.DataLength) ||
                     fragmentCount != inCmd.fragmentCount)
                    return -1;

                startCommand = inCmd;
                break;
            }
        }

        if (startCommand == null)
        {
            startCommand = peer.QueueInCmd(commandHeader, null, totalLength, (int)CSENetPacketFlag.UnreliableFragment, fragmentCount, sendFragmentCmd.startSeqNum);
            if (startCommand == null)
                return -1;
        }

        if (startCommand.fragments != null && (startCommand.fragments[fragmentNumber / 32] & (1 << ((int)fragmentNumber % 32))) == 0)
        {
            --startCommand.fragmentsRemaining;

            startCommand.fragments[fragmentNumber / 32] |= (uint)(1 << ((int)fragmentNumber % 32));

            if (startCommand.packet != null && fragmentOffset + fragmentLength > startCommand.packet.DataLength)
                fragmentLength = startCommand.packet.DataLength - fragmentOffset;

            if (startCommand.packet != null && startCommand.packet.Data != null)
            {
                Array.Copy(startCommand.packet.Data, fragmentOffset, this.receivedData, currentDataIdx, fragmentLength);
            }

            if (startCommand.fragmentsRemaining <= 0)
                peer.DispatchInUnreliableCmds(channel, null);
        }

        return 0;

    }

    #endregion

}
