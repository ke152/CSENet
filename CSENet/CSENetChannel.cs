namespace CSENet;

public class CSENetChannel
{
    public uint outReliableSeqNum = 0;
    public uint outUnreliableSeqNum = 0;
    public int usedReliableWindows = 0;
    public uint[] reliableWindows = new uint[(int)CSENetDef.PeerReliableWindows];
    public uint inReliableSeqNum = 0;
    public uint inUnreliableSeqNum = 0;
    public List<CSENetInCmd> inReliableCmds = new();
    public List<CSENetInCmd> inUnreliableCmds = new();

    public CSENetChannel()
    {

    }
}
