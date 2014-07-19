using System;
using EventStore.Common.Utils;

namespace EventStore.Core.Services.Monitoring.Stats
{
    public class QueueStats
    {
        public readonly string Name;
        public readonly string GroupName;
        public readonly int Length;
        public readonly long LengthLifetimePeak;
        public readonly long LengthCurrentTryPeak;
        public readonly TimeSpan? CurrentItemProcessingTime;
        public readonly TimeSpan? CurrentIdleTime;
        public readonly long TotalItemsProcessed;
        public readonly int AvgItemsPerSecond;
        public readonly double AvgProcessingTime;
        public readonly double IdleTimePercent;
        public readonly Type LastProcessedMessageType;
        public readonly Type InProgressMessageType;

        public QueueStats(string name,
                          string groupName,
                          int length,
                          int avgItemsPerSecond,
                          double avgProcessingTime,
                          double idleTimePercent,
                          TimeSpan? currentItemProcessingTime,
                          TimeSpan? currentIdleTime,
                          long totalItemsProcessed, 
                          long lengthCurrentTryPeak, 
                          long lengthLifetimePeak,
                          Type lastProcessedMessageType,
                          Type inProgressMessageType)
        {
            Name = name;
            GroupName = groupName;
            Length = length;
            AvgItemsPerSecond = avgItemsPerSecond;
            AvgProcessingTime = avgProcessingTime;
            IdleTimePercent = idleTimePercent;
            CurrentItemProcessingTime = currentItemProcessingTime;
            CurrentIdleTime = currentIdleTime;
            TotalItemsProcessed = totalItemsProcessed;
            LengthCurrentTryPeak = lengthCurrentTryPeak;

            LengthLifetimePeak = lengthLifetimePeak;

            LastProcessedMessageType = lastProcessedMessageType;
            InProgressMessageType = inProgressMessageType;
        }

        public override string ToString()
        {
            var str = string.Format("{0,-22} L: {1,-5}      Avg: {5,-5}i/s    AvgProcTime: {6:0.0}ms\n"
                                    + "      Idle %:{7,-5:00.0}  Peak: {2,-5}  MaxPeak: {3,-7}  TotalProcessed: {4,-7}\n" 
                                    + "      Processing: {8}, Last: {9}",
                                    Name,
                                    Length,
                                    LengthCurrentTryPeak,
                                    LengthLifetimePeak,
                                    TotalItemsProcessed,
                                    AvgItemsPerSecond,
                                    AvgProcessingTime,
                                    IdleTimePercent,
                                    InProgressMessageType == null ? "<none>" : InProgressMessageType.Name,
                                    LastProcessedMessageType == null ? "<none>" : LastProcessedMessageType.Name);
            return str;
        }
    }
}