using System.Text;
using Newtonsoft.Json;

namespace ENet;

internal static class Serialize
{
    // 由结构体转换为byte数组
    public static byte[] ToBytes<T>(T t)
    {
        return Encoding.Default.GetBytes(JsonConvert.SerializeObject(t));
    }

    /// 由byte数组转换为结构体
    public static T? FromBytes<T>(byte[] bytes)
    {
        return JsonConvert.DeserializeObject<T>(Encoding.Default.GetString(bytes));
    }
}
