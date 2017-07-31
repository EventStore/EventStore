using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventStore.Projections.Core.Services.Management
{
    public static class ProjectionConsts
    {
        public const int CheckpointHandledThreshold = 4000;
        public const int PendingEventsThreshold = 5000;
        public const int MaxWriteBatchLength = 500;
        public const int CheckpointUnhandledBytesThreshold = 10 * 1000 * 1000;
        public const int MaxAllowedWritesInFlight = 1;
        public static TimeSpan CheckpointAfterMs = TimeSpan.FromSeconds(5);
    }
}
