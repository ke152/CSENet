using System.Net;
using System.Runtime.InteropServices;

namespace CSENet;

public enum CSENetPeerState
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

public class CSENetPeer
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


    public void QueueOutgoingCommand(CSENetProto cmd, CSENetPacket? packet, uint offset, uint length)
    {
        CSENetOutCmd outCmd = new();
        outCmd.cmd = cmd;
        outCmd.fragmentOffset = offset;
        outCmd.fragmentLength = length;
        outCmd.packet = packet;

        SetupOutCmd(outCmd);
    }

    public void SetupOutCmd(CSENetOutCmd outCmd)
    {
        outDataTotal += (int)CSENetProtoCmdSize.CmdSize[Convert.ToInt32(outCmd.cmdHeader.cmdFlag & (int)CSENetProtoCmdType.Mask)] + (int)outCmd.fragmentLength;

        if (outCmd.cmdHeader.channelID == 0xFF)
        {
            ++outReliableSeqNum;

            outCmd.reliableSeqNum = outReliableSeqNum;
            outCmd.unreliableSeqNum = 0;
        }
        else if (channels != null)
        {
            CSENetChannel channel = channels[outCmd.cmdHeader.channelID];

            if ((outCmd.cmdHeader.cmdFlag & (int)CSENetProtoFlag.CmdFlagAck) != 0)
            {
                ++channel.outReliableSeqNum;
                channel.outUnreliableSeqNum = 0;

                outCmd.reliableSeqNum = channel.outReliableSeqNum;
                outCmd.unreliableSeqNum = 0;
            }
            else
            {
                if ((outCmd.cmdHeader.cmdFlag & (int)CSENetProtoFlag.CmdFlagUnSeq) != 0)
                {
                    ++outUnSeqGroup;

                    outCmd.reliableSeqNum = 0;
                    outCmd.unreliableSeqNum = 0;
                }
                else
                {
                    if (outCmd.fragmentOffset == 0)
                        ++channel.outUnreliableSeqNum;

                    outCmd.reliableSeqNum = channel.outReliableSeqNum;
                    outCmd.unreliableSeqNum = channel.outUnreliableSeqNum;
                }
            }
        }

        outCmd.sendAttempts = 0;
        outCmd.sentTime = 0;
        outCmd.rttTimeout = 0;
        outCmd.rttTimeoutLimit = 0;
        outCmd.cmdHeader.reliableSeqNum = CSENetUtils.HostToNetOrder(outCmd.reliableSeqNum);

        switch (outCmd.cmdHeader.cmdFlag & (int)CSENetProtoCmdType.Mask)
        {
            case (int)CSENetProtoCmdType.SendUnreliable:
                if (outCmd.cmd.sendUnReliable != null)
                    outCmd.cmd.sendUnReliable.unreliableSeqNum = CSENetUtils.HostToNetOrder(outCmd.unreliableSeqNum);
                break;

            case (int)CSENetProtoCmdType.SendUnseq:
                if (outCmd.cmd.sendUnseq != null)
                    outCmd.cmd.sendUnseq.unseqGroup = CSENetUtils.HostToNetOrder(outUnSeqGroup);
                break;

            default:
                break;
        }
        outCmds.Add(outCmd);
    }

    //TODO:处理所有0引用的函数
    //TODO:直接Clear不需要进行函数调用
    public static void ResetCmds(List<CSENetOutCmd> list)
    {
        list.Clear();
    }

    public static void RemoveInCmds(List<CSENetInCmd> list, CSENetInCmd? startCmd, CSENetInCmd? endCmd, CSENetInCmd? excludeCmd)
    {
        if (list == null || startCmd == null || endCmd == null) return;
        if (list.Count == 0) return;

        CSENetInCmd currCmd;
        int i = 0;
        bool canDelete = false;
        while (i < list.Count)
        {
            currCmd = list[i];
            if (currCmd == startCmd) canDelete = true;
            if (currCmd == endCmd) canDelete = false;

            if (canDelete && currCmd != excludeCmd)
            {
                list.Remove(currCmd);
                continue;
            }

            i++;
        }
    }

    public void OnConnect()
    {
        if (state !=CSENetPeerState.Connected && state != CSENetPeerState.DisconnectLater)
        {
            if (this.host == null) return;

            if (inBandwidth != 0)
                ++this.host.bandwidthLimitedPeers;

            ++this.host.connectedPeers;
        }
    }

    public void QueueAck(CSENetProtoCmdHeader cmdHeader, uint sentTime)
    {
        if (cmdHeader.channelID < channels?.Length)
        {
            CSENetChannel channel = channels[cmdHeader.channelID];
            uint reliableWindow = cmdHeader.reliableSeqNum / Convert.ToUInt32(CSENetDef.PeerReliableWindowSize),
                        currentWindow = channel.inReliableSeqNum / Convert.ToUInt32(CSENetDef.PeerReliableWindowSize);

            if (cmdHeader.reliableSeqNum < channel.inReliableSeqNum)
                reliableWindow += Convert.ToUInt32(CSENetDef.PeerReliableWindows);

            if (reliableWindow >= currentWindow + Convert.ToUInt32(CSENetDef.PeerReliableWindows) - 1 && reliableWindow <= currentWindow + Convert.ToUInt32(CSENetDef.PeerReliableWindows))
                return;
        }

        CSENetAckCmd ack = new();
        ack.sentTime = sentTime;
        ack.cmdHeader = cmdHeader;

        this.outDataTotal += Marshal.SizeOf<CSENetAckCmd>();

        acknowledgements.Add(ack);
    }

    //TODO：这个函数可能应该交给channel
    public void DispatchInUnreliableCmds(CSENetChannel channel, CSENetInCmd? queuedCmd)
    {
        if (channel.inUnreliableCmds.Count == 0) return;

        CSENetInCmd startCmd = channel.inUnreliableCmds.First();
        CSENetInCmd? droppedCmd = startCmd;
        CSENetInCmd currentCmd = startCmd;

        int i;
        for (i = 0; i < channel.inUnreliableCmds.Count; i++)
        {
            CSENetInCmd inCmd = currentCmd;

            if ((inCmd.cmdHeader.cmdFlag & (int)CSENetProtoCmdType.Mask) == (int)CSENetProtoCmdType.SendUnseq)
                continue;

            if (inCmd.reliableSeqNum == channel.inReliableSeqNum)
            {
                if (inCmd.fragmentsRemaining <= 0)
                {
                    channel.inUnreliableSeqNum = inCmd.unreliableSeqNum;
                    continue;
                }

                if (startCmd != currentCmd)
                {
                    for (int j = 0; j < i; j++)
                    {
                        dispatchedCmds.Add(channel.inUnreliableCmds[j]);
                    }

                    if (!needDispatch)
                    {
                        this.host?.dispatchQueue.Add(this);
                        needDispatch = true;
                    }

                    droppedCmd = currentCmd;
                }
                else
                if (droppedCmd != currentCmd && i > 0)
                    droppedCmd = channel.inUnreliableCmds[i - 1];
            }
            else
            {
                ushort reliableWindow = (ushort)(inCmd.reliableSeqNum / (ushort)CSENetDef.PeerReliableWindowSize),
                            currentWindow = (ushort)(channel.inReliableSeqNum / (ushort)CSENetDef.PeerReliableWindowSize);
                if (inCmd.reliableSeqNum < channel.inReliableSeqNum)
                    reliableWindow += (ushort)CSENetDef.PeerReliableWindows;
                if (reliableWindow >= currentWindow && reliableWindow < currentWindow + (ushort)CSENetDef.PeerFreeReliableWindows - 1)
                    break;

                if (i < channel.inUnreliableCmds.Count)
                {
                    droppedCmd = channel.inUnreliableCmds[i + 1];
                }
                else
                {
                    droppedCmd = null;
                }

                if (startCmd != currentCmd)
                {
                    for (int j = 0; j < i; j++)
                    {
                        dispatchedCmds.Add(channel.inUnreliableCmds[j]);
                    }

                    if (!needDispatch)
                    {
                        this.host?.dispatchQueue.Add(this);
                        needDispatch = true;
                    }
                }
            }
        }

        if (startCmd != currentCmd)
        {
            for (int j = 0; j < i; j++)
            {
                dispatchedCmds.Add(channel.inUnreliableCmds[j]);
            }

            if (!needDispatch)
            {
                this.host?.dispatchQueue.Add(this);
                needDispatch = true;
            }

            droppedCmd = currentCmd;
        }

        RemoveInCmds(channel.inUnreliableCmds, startCmd, droppedCmd, queuedCmd);
    }

    public void DispatchInReliableCmds(CSENetChannel channel, CSENetInCmd? queuedCmd)
    {
        if (channel.inReliableCmds.Count == 0) return;

        CSENetInCmd currentCmd = channel.inReliableCmds[0];

        int i;
        for (i = 0; i < channel.inReliableCmds.Count; i++)
        {
            currentCmd = channel.inReliableCmds[i];

            if (currentCmd.fragmentsRemaining > 0 ||
                currentCmd.reliableSeqNum != channel.inReliableSeqNum + 1)
                break;

            channel.inReliableSeqNum = currentCmd.reliableSeqNum;

            if (currentCmd.fragmentCount > 0)
                channel.inReliableSeqNum += currentCmd.fragmentCount - 1;
        }

        if (currentCmd == null) return;

        channel.inUnreliableSeqNum = 0;
        for (int j = 0; j < i; j++)
        {
            dispatchedCmds.Add(channel.inReliableCmds[j]);
        }

        if (!this.needDispatch)
        {
            this.host?.dispatchQueue.Add(this);
            needDispatch = true;
        }

        DispatchInUnreliableCmds(channel, queuedCmd);
    }

    public int Send(uint channelID, CSENetPacket packet)
    {
        CSENetChannel channel;
        CSENetProto cmd = new();
        uint fragmentLength;

        if (this.state != CSENetPeerState.Connected ||
            channelID >= this.channels?.Length
            || this.host == null || packet.DataLength > this.host.maximumPacketSize)
        {
            return -1;
        }

        if (channels == null) return -1;

        channel = channels[Convert.ToInt32(channelID)];
        fragmentLength = mtu - Convert.ToUInt32(Marshal.SizeOf<CSENetProtoHeader>() + Marshal.SizeOf<CSENetProtoSendFragment>());

        //分片
        if (packet.DataLength > fragmentLength)
        {
            uint fragmentCount = (packet.DataLength + fragmentLength - 1) / fragmentLength,
                    fragmentNumber,
                    fragmentOffset;
            int cmdNum;
            uint startSequenceNumber;
            List<CSENetOutCmd> fragments = new();
            CSENetOutCmd fragment;

            if (fragmentCount > CSENetDef.ProtoMaxFragmentCount)
                return -1;

            if ((packet.Flags & ((int)CSENetPacketFlag.UnreliableFragment | (int)CSENetPacketFlag.Reliable)) == (int)CSENetPacketFlag.UnreliableFragment &&
                channel.outUnreliableSeqNum < 0xFFFF)
            {
                cmdNum = (int)CSENetProtoCmdType.SendUnreliableFragment;
                startSequenceNumber = (uint)IPAddress.HostToNetworkOrder(channel.outUnreliableSeqNum + 1);
            }
            else
            {
                cmdNum = (int)CSENetProtoCmdType.SendFragment | (int)CSENetProtoFlag.CmdFlagAck;
                startSequenceNumber = (uint)IPAddress.HostToNetworkOrder(channel.outUnreliableSeqNum + 1);
            }

            for (fragmentNumber = 0,
                    fragmentOffset = 0;
                fragmentOffset < packet.DataLength;
                ++fragmentNumber,
                    fragmentOffset += fragmentLength)
            {
                if (packet.DataLength - fragmentOffset < fragmentLength)
                    fragmentLength = packet.DataLength - fragmentOffset;

                fragment = new();

                fragment.fragmentOffset = fragmentOffset;
                fragment.fragmentLength = fragmentLength;
                fragment.packet = packet;
                fragment.cmdHeader.cmdFlag = cmdNum;
                fragment.cmdHeader.channelID = channelID;

                fragment.cmd.sendFragment = new();
                fragment.cmd.sendFragment.startSeqNum = startSequenceNumber;
                fragment.cmd.sendFragment.dataLength = (uint)IPAddress.HostToNetworkOrder(fragmentLength);
                fragment.cmd.sendFragment.fragmentCount = (uint)IPAddress.HostToNetworkOrder(fragmentCount);
                fragment.cmd.sendFragment.fragmentNum = (uint)IPAddress.HostToNetworkOrder(fragmentNumber);
                fragment.cmd.sendFragment.totalLength = (uint)IPAddress.HostToNetworkOrder(packet.DataLength);
                fragment.cmd.sendFragment.fragmentOffset = (uint)IPAddress.NetworkToHostOrder(fragmentOffset);

                fragments.Add(fragment);
            }

            while (fragments.Count > 0)
            {
                fragment = fragments[0];
                fragments.RemoveAt(0);

                SetupOutCmd(fragment);
            }

            return 0;
        }
        //不用分片的

        cmd.header.channelID = channelID;

        if ((packet.Flags & ((int)CSENetPacketFlag.Reliable | (int)CSENetPacketFlag.UnSeq)) == (int)CSENetPacketFlag.UnSeq)
        {
            cmd.header.cmdFlag = (int)CSENetProtoCmdType.SendUnseq | (int)CSENetProtoFlag.CmdFlagUnSeq;
            cmd.sendUnseq = new();
            cmd.sendUnseq.dataLength = (uint)IPAddress.HostToNetworkOrder(packet.DataLength);
        }
        else
        {
            cmd.sendReliable = new();
            if ((packet.Flags & (int)CSENetPacketFlag.Reliable) != 0 || channel.outUnreliableSeqNum >= 0xFFFF)
            {
                cmd.header.cmdFlag = (int)CSENetProtoCmdType.SendReliable | (int)CSENetProtoFlag.CmdFlagAck;
                cmd.sendReliable.dataLength = (uint)IPAddress.HostToNetworkOrder(packet.DataLength);
            }
            else
            {
                cmd.header.cmdFlag = (int)CSENetProtoCmdType.SendReliable;
                cmd.sendReliable.dataLength = (uint)IPAddress.HostToNetworkOrder(packet.DataLength);
            }
        }

        QueueOutgoingCommand(cmd, packet, 0, packet.DataLength);

        return 0;
    }

    public CSENetPacket? Receive(ref uint channelID)
    {
        CSENetInCmd inCmd;
        CSENetPacket? packet;

        if (this.dispatchedCmds == null || this.dispatchedCmds.Count == 0)
            return null;

        inCmd = this.dispatchedCmds.First();

        channelID = inCmd.cmdHeader.channelID;

        packet = inCmd.packet;

        if (packet != null)
        {
            this.totalWaitingData -= packet.DataLength;
        }

        return packet;
    }
    public void Ping()
    {
        CSENetProto command = new();

        if (this.state != CSENetPeerState.Connected)
            return;

        command.header.cmdFlag = (int)CSENetProtoCmdType.Ping | (int)CSENetProtoFlag.CmdFlagAck;//TODO: cmdFlag改成2个不同的标志位
        command.header.channelID = 0xFF;

        QueueOutgoingCommand(command, null, 0, 0);
    }

    public void PingInterval(uint pingInterval)//TODO: rename setPingInxxxx
    {
        this.pingInterval = pingInterval != 0 ? pingInterval : CSENetDef.PeerPingInterval;
    }

    public void Timeout(uint timeoutLimit, uint timeoutMinimum, uint timeoutMaximum)//TODO: rename setTimeout
    {
        this.timeoutLimit = timeoutLimit != 0 ? timeoutLimit : CSENetDef.PeerTimeoutLimit;
        this.timeoutMinimum = timeoutMinimum != 0 ? timeoutMinimum : CSENetDef.PeerTimeoutMin;
        this.timeoutMaximum = timeoutMaximum != 0 ? timeoutMaximum : CSENetDef.PeerTimeoutMax;
    }

    public CSENetInCmd? QueueInCmd(CSENetProtoCmdHeader cmdHeader, byte[]? data, uint dataLength, int flags, uint fragmentCount, uint sendUnreliableSeqNum = 0)
    {
        CSENetInCmd dummyCmd = new();

        if (channels == null) return null;
        CSENetChannel channel = channels[cmdHeader.channelID];
        uint unreliableSeqNum = 0, reliableSeqNum = 0;
        uint reliableWindow, currentWindow;
        CSENetInCmd inCmd;
        CSENetInCmd? currCmd = null;
        CSENetPacket packet;

        if (state == CSENetPeerState.DisconnectLater)
            goto discardcmd;

        if ((cmdHeader.cmdFlag & (int)CSENetProtoCmdType.SendUnseq) != 0)
        {
            reliableSeqNum = cmdHeader.reliableSeqNum;
            reliableWindow = reliableSeqNum / CSENetDef.PeerReliableWindowSize;
            currentWindow = channel.inReliableSeqNum / CSENetDef.PeerReliableWindowSize;

            if (reliableSeqNum < channel.inReliableSeqNum)
                reliableWindow += CSENetDef.PeerReliableWindows;

            if (reliableWindow < currentWindow || reliableWindow >= currentWindow + CSENetDef.PeerReliableWindows - 1)
                goto discardcmd;
        }

        switch (cmdHeader.cmdFlag & (int)CSENetProtoCmdType.Mask)
        {
            case (int)CSENetProtoCmdType.SendFragment:
            case (int)CSENetProtoCmdType.SendReliable:
                if (reliableSeqNum == channel.inReliableSeqNum)
                    goto discardcmd;

                for (int i = channel.inReliableCmds.Count; i >= 0; i--)
                {
                    currCmd = channel.inReliableCmds[i];
                    inCmd = currCmd;

                    if (reliableSeqNum >= channel.inReliableSeqNum)
                    {
                        if (inCmd.reliableSeqNum < channel.inReliableSeqNum)
                            continue;
                    }
                    else
                    if (inCmd.reliableSeqNum >= channel.inReliableSeqNum)
                        break;

                    if (inCmd.reliableSeqNum <= reliableSeqNum)
                    {
                        if (inCmd.reliableSeqNum < reliableSeqNum)
                            break;

                        goto discardcmd;
                    }
                }
                break;

            case (int)CSENetProtoCmdType.SendUnreliable:
            case (int)CSENetProtoCmdType.SendUnreliableFragment:
                unreliableSeqNum = CSENetUtils.NetToHostOrder(sendUnreliableSeqNum);

                if (reliableSeqNum == channel.inReliableSeqNum &&
                    unreliableSeqNum <= channel.inUnreliableSeqNum)
                    goto discardcmd;

                for (int i = channel.inUnreliableCmds.Count; i >= 0; i--)
                {
                    currCmd = channel.inReliableCmds[i];
                    inCmd = currCmd;

                    if ((cmdHeader.cmdFlag & (int)CSENetProtoCmdType.Mask) == (int)CSENetProtoCmdType.SendUnreliable)
                        continue;

                    if (reliableSeqNum >= channel.inReliableSeqNum)
                    {
                        if (inCmd.reliableSeqNum < channel.inReliableSeqNum)
                            continue;
                    }
                    else
                    if (inCmd.reliableSeqNum >= channel.inReliableSeqNum)
                        break;

                    if (inCmd.reliableSeqNum < reliableSeqNum)
                        break;

                    if (inCmd.reliableSeqNum > reliableSeqNum)
                        continue;

                    if (inCmd.unreliableSeqNum <= unreliableSeqNum)
                    {
                        if (inCmd.unreliableSeqNum < unreliableSeqNum)
                            break;

                        goto discardcmd;
                    }
                }
                break;

            case (int)CSENetProtoCmdType.SendUnseq:
                currCmd = channel.inUnreliableCmds.Last();
                break;

            default:
                goto discardcmd;
        }

        if (totalWaitingData >= this.host?.maximumWaitingData)
            goto notifyError;

        packet = new(data, flags);
        if (packet == null)
            goto notifyError;

        inCmd = new();

        inCmd.reliableSeqNum = cmdHeader.reliableSeqNum;
        inCmd.unreliableSeqNum = unreliableSeqNum & 0xFFFF;
        inCmd.cmdHeader = cmdHeader;
        inCmd.fragmentCount = fragmentCount;
        inCmd.fragmentsRemaining = fragmentCount;
        inCmd.packet = packet;
        inCmd.fragments = null;

        if (fragmentCount > 0 && fragmentCount <= CSENetDef.ProtoMaxFragmentCount)
            inCmd.fragments = new uint[(fragmentCount + 31) / 32];

        if (packet != null && packet.Data != null)
        {
            totalWaitingData += Convert.ToUInt32(packet.Data.Length);
        }

        if (currCmd != null)
        {
            channel.inReliableCmds.Insert(channel.inReliableCmds.IndexOf(currCmd) + 1, inCmd);
        }

        switch (cmdHeader.cmdFlag & (int)CSENetProtoCmdType.Mask)
        {
            case (int)CSENetProtoCmdType.SendFragment:
            case (int)CSENetProtoCmdType.SendReliable:
                DispatchInReliableCmds(channel, inCmd);
                break;
            default:
                DispatchInUnreliableCmds(channel, inCmd);
                break;
        }

        return inCmd;

    discardcmd:
        if (fragmentCount > 0)
            goto notifyError;

        return dummyCmd;

    notifyError:
        return null;
    }

}
