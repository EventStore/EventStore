using System;

namespace EventStore.Core.Services.RequestManager.Managers
{
    public static class Timeouts
    {
        public static readonly TimeSpan PrepareTimeout = TimeSpan.FromMilliseconds(2000);
        public static readonly TimeSpan CommitTimeout = TimeSpan.FromMilliseconds(2000);
    }
}