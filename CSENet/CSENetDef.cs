namespace CSENet;

class CSENetDef
{
    // host
    public const uint HostRecvBufferSize = 256 * 1024;
    public const uint HostSendBufferSize = 256 * 1024;
    public const uint HostBandwidthThrottleInterval = 1000;
    public const uint HostDefaultMTU = 1400;
    public const uint HostDefaultMaxPacketSize = 32 * 1024 * 1024;
    public const uint HostDefaultMaxWaintingData = 32 * 1024 * 1024;

    // peer
    public const uint PeerDefaultRTT = 500;
    public const uint PeerDefaultPacketThrottle = 32;
    public const uint PeerPacketThrottleScale = 32;
    public const uint PeerPacketThrottleCounter = 7;
    public const uint PeerPacketThrottleAcceleration = 2;
    public const uint PeerPacketThrottleDeceleration = 2;
    public const uint PeerPacketThrottleInterval = 5000;
    public const uint PeerPacketLossScale = (1 << 16);
    public const uint PeerPacketLossInterval = 10000;
    public const uint PeerWindowSizeScale = 64 * 1024;
    public const uint PeerTimeoutLimit = 32;
    public const uint PeerTimeoutMin = 5000;
    public const uint PeerTimeoutMax = 30000;
    public const uint PeerPingInterval = 500;
    public const uint PeerUnseqWindows = 64;
    public const uint PeerUnseqWindowSize = 1024;
    public const uint PeerFreeUnseqWindows = 32;
    public const uint PeerReliableWindows = 16;
    public const uint PeerReliableWindowSize = 0x1000;
    public const uint PeerFreeReliableWindows = 8;

    // proto
    public const uint ProtoMinMTU = 576;
    public const uint ProtoMaxMTU = 4096;
    public const uint ProtoMaxPacketCmds = 32;
    public const uint ProtoMinWindowSize = 4096;
    public const uint ProtoMaxWindowSize = 65536;
    public const uint ProtoMinChannelCount = 1;
    public const uint ProtoMaxChannelCount = 255;
    public const uint ProtoMaxPeerID = 0xFFF;
    public const uint ProtoMaxFragmentCount = 1024 * 1024;

    //buffer
    public const uint BufferMax = 1 + 2 * ProtoMaxPacketCmds;

};
