namespace CSENet;

public enum CSENetEventType
{
    None = 0,
    Connect = 1,
    Disconnect = 2,
    Recv = 3
}

public class CSENetEvent
{
        public CSENetEventType type = CSENetEventType.None;      /**< type of the event */
        public CSENetPeer? peer = null;      /**< peer that generated a connect, disconnect or receive event */
        public uint channelID; /**< channel on the peer that generated the event, if appropriate */
        public uint data;      /**< data associated with the event, if appropriate */
        public CSENetPacket? packet = null;    /**< packet associated with the event, if appropriate */

        public CSENetEvent()
        {

        }

}
