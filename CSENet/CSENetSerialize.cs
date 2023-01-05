using System.Text;
using Newtonsoft.Json;
using NLog;

namespace ENet;

internal static class Serialize
{
    internal static readonly Logger logger = LogManager.GetCurrentClassLogger();

    // 由结构体转换为byte数组
    public static byte[] ToBytes<T>(T t)
    {
        logger.Debug("Serialize::ToBytes");
        return Encoding.Default.GetBytes(JsonConvert.SerializeObject(t));
    }

    /// 由byte数组转换为结构体
    public static T? FromBytes<T>(byte[] bytes)
    {
        logger.Debug("Serialize::FromBytes");
        return JsonConvert.DeserializeObject<T>(Encoding.Default.GetString(bytes));
    }
}
