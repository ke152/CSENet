namespace CSENet;


enum CSENetPacketFlag
{
    // TODO：if(flags & ENetPacketFlag.Reliable)  报错&结果为int，不能转bool
    //必须由对应peer接收；在确认发出前必须不断重发
    Reliable = (1 << 0),
    // 无序；和reliable互斥
    UnSeq = (1 << 1),
    // 数据部分由用户提供，程序不会分配内存
    NoAllocate = (1 << 2),//由于c#的引用机制，不需要memcpy。这个标志位应该没用了
                          // 如果超过MTU，数据包会分多个片段以unreliable（代替reliable）发出（TODO：这个替代的逻辑，没注意到）
    UnreliableFragment = (1 << 3),
    // packet在所有队列中的引用都被发出
    Sent = (1 << 8)
}

public class CSENetPacket
{
    /**
    * An ENet data packet that may be sent to or received from a peer. The shown 
    * fields should only be read and never modified. 
    */
    public int Flags;           /**< bitwise-or of ENetPacketFlag constants */
    public byte[]? Data;            //allocated data for packet
    public uint DataLength { get { return this.Data == null ? 0 : Convert.ToUInt32(this.Data.Length); } }

    public CSENetPacket(byte[]? data, int flags)
    {
        this.Data = data;
        this.Flags = flags;
    }

    public void Resize(uint dataLength)
    {
        this.Data = new byte[dataLength];
    }
}
