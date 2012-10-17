using System;

namespace EventStore.Core.Services.RequestManager.Managers
{
    public static class Timeouts
    {
        public static readonly TimeSpan PrepareTimeout = TimeSpan.FromMilliseconds(2000);
        public static readonly TimeSpan PrepareWriteMessageTimeout = TimeSpan.FromMilliseconds(PrepareTimeout.TotalMilliseconds * 0.9);
        
        public static readonly TimeSpan CommitTimeout = TimeSpan.FromMilliseconds(2000);
        public static readonly TimeSpan CommitWriteMessageTimeout = TimeSpan.FromMilliseconds(CommitTimeout.TotalMilliseconds * 0.9);
    }
}