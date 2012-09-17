using System;

namespace EventStore.Core.Messages
{
    public interface IAmOnlyCaredAboutForTime
    {
        DateTime LiveUntil { get; }
    }

    public static class IAmOnlyCaredAboutForTimeExtensions
    {
        public static bool AmStillCaredAbout(this IAmOnlyCaredAboutForTime msg)
        {
            return msg.LiveUntil > DateTime.UtcNow;
        }
    }
}