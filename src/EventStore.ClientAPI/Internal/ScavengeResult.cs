using System;

namespace EventStore.ClientAPI.Internal
{
    /// <summary>
    /// Result of a successful scavenge operation
    /// </summary>
    public class ScavengeResult
    {
        /// <summary>
        /// Amount of time taken to complete the scavenging operation
        /// </summary>
        public readonly TimeSpan TotalTime;
        /// <summary>
        /// Total space saved, in bytes
        /// </summary>
        public readonly long TotalSpaceSaved;

        internal ScavengeResult(TimeSpan totalTime, long totalSpaceSaved)
        {
            TotalTime = totalTime;
            TotalSpaceSaved = totalSpaceSaved;
        }
    }
}