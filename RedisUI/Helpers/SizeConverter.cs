using System;

namespace RedisUI.Helpers
{
    static class SizeConverter
    {
        internal static string ToKilobytes(this long bytes) =>
            (bytes / Math.Pow(1024, 1)).ToString("0.00");

        internal static string ToMegabytes(this long bytes) =>
            (bytes / Math.Pow(1024, 2)).ToString("0.00");
    }
}
