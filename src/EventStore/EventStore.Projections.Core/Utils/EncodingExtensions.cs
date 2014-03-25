using EventStore.Common.Utils;

namespace EventStore.Projections.Core.Utils
{
    public static class EncodingExtensions
    {
        public static string FromUtf8(this byte[] self)
        {
            return Helper.UTF8NoBom.GetString(self);
        }

        public static byte[] ToUtf8(this string self)
        {
            return Helper.UTF8NoBom.GetBytes(self);
        }
    }
}
