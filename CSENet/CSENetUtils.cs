using Newtonsoft.Json;
using System.Net;
using System.Text;

namespace CSENet;

class CSENetUtils
{
    private static DateTime timeStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static DateTime timeBase = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static long TimeGet()
    {
        return (long)(DateTime.UtcNow - timeBase).TotalMilliseconds;
    }
    public static void TimeSet(ulong newTimeBase)
    {
        timeBase = timeStart.AddMilliseconds(newTimeBase);
    }
    public static uint RandomSeed()
    {
        return (uint)TimeGet();
    }

    public static uint HostToNetOrder(uint value)
    {
        return Convert.ToUInt32(IPAddress.HostToNetworkOrder(Convert.ToInt32(value)));
    }
    public static uint NetToHostOrder(uint value)
    {
        return Convert.ToUInt32(IPAddress.NetworkToHostOrder(Convert.ToInt32(value)));
    }


    public static byte[] Serialize<T>(T msg)
    {
        return Encoding.Default.GetBytes(JsonConvert.SerializeObject(msg));
    }

    public static T? DeSerialize<T>(byte[] bytes)
    {
        return JsonConvert.DeserializeObject<T>(Encoding.Default.GetString(bytes));
    }

    public static byte[] SubBytes(byte[] data, int start, int length)
    {
        if (start < 0 || start >= data.Length || (start == 0 && length >= data.Length))
        {
            return data;
        }

        if (start + length > data.Length)
        {
            length = data.Length - start;
        }

        byte[] result = new byte[length];

        for (int i = start; i < start + length; i++)
        {
            result[i - start] = data[i];
        }

        return result;
    }
}


