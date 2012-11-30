// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
using System;
using EventStore.Common.Utils;

namespace EventStore.Core.Services.Monitoring.Stats
{
    public class QueueStats
    {
        public readonly string Name;
        public readonly int Length;
        public readonly long LengthLifetimePeak;
        public readonly long LengthCurrentTryPeak;
        public readonly string LengthLifetimePeakFriendly;
        public readonly TimeSpan? CurrentItemProcessingTime;
        public readonly TimeSpan? CurrentIdleTime;
        public readonly long TotalItemsProcessed;
        public readonly int AvgItemsPerSecond;
        public readonly double AvgProcessingTime;
        public readonly double IdleTimePercent;
        public readonly Type LastProcessedMessageType;
        public readonly Type InProgressMessageType;

        public QueueStats(string name,
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
            Length = length;
            AvgItemsPerSecond = avgItemsPerSecond;
            AvgProcessingTime = avgProcessingTime;
            IdleTimePercent = idleTimePercent;
            CurrentItemProcessingTime = currentItemProcessingTime;
            CurrentIdleTime = currentIdleTime;
            TotalItemsProcessed = totalItemsProcessed;
            LengthCurrentTryPeak = lengthCurrentTryPeak;

            LengthLifetimePeak = lengthLifetimePeak;
            LengthLifetimePeakFriendly = lengthLifetimePeak.ToFriendlyNumberString();

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