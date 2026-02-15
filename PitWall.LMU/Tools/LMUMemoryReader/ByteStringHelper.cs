using System;
using System.Text;

namespace LMUMemoryReader;

public static class ByteStringHelper
{
    public static string FromNullTerminated(byte[]? data)
    {
        if (data == null || data.Length == 0)
        {
            return string.Empty;
        }

        var length = Array.IndexOf(data, (byte)0);
        if (length < 0)
        {
            length = data.Length;
        }

        return Encoding.UTF8.GetString(data, 0, length).Trim();
    }
}
