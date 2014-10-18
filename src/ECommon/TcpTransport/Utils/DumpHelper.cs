using System;
using System.Text;

namespace ECommon.TcpTransport.Utils
{
    public static class DumpHelper
    {
        public static string FormatBinaryDump(ArraySegment<byte> logBulk)
        {
            if (logBulk.Count == 0)
                return "--- NO DATA ---";

            var sb = new StringBuilder();
            int cur = 0;
            int len = logBulk.Count;
            for (int row = 0, rows = (logBulk.Count + 15) / 16; row < rows; ++row)
            {
                sb.AppendFormat("{0:000000}:", row * 16);
                for (int i = 0; i < 16; ++i, ++cur)
                {
                    if (cur >= len)
                        sb.Append("   ");
                    else
                        sb.AppendFormat(" {0:X2}", logBulk.Array[logBulk.Offset + cur]);
                }
                sb.Append("  | ");
                cur -= 16;
                for (int i = 0; i < 16; ++i, ++cur)
                {
                    if (cur < len)
                    {
                        var b = (char)logBulk.Array[logBulk.Offset + cur];
                        sb.Append(char.IsControl(b) ? '.' : b);
                    }
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
