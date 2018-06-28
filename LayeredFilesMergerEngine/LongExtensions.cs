using System;

namespace LayeredFilesMergerEngine
{
    public static class LongExtensions
    {
        public static string GetSizeString(this long? size)
        {
            if (size == null)
                return string.Empty;

            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
            if (size.Value == 0)
                return "0" + suf[0];
            var bytes = Math.Abs(size.Value);
            var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            var num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(size.Value) * num) + suf[place];
        }
    }
}
