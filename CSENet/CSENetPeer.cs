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
}
