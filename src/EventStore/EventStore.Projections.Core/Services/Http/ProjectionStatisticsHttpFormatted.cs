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
using System.Security.Policy;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Services.Http
{
    public class ProjectionStatisticsHttpFormatted
    {
        public ProjectionStatisticsHttpFormatted(ProjectionStatistics source, Func<string, string> makeAbsoluteUrl)
        {
            this.Status = source.Status;
            this.StateReason = source.StateReason;
            this.Name = source.Name;
            this.EffectiveName = source.Name;
            this.Epoch = source.Epoch;
            this.Version = source.Version;
            this.Mode = source.Mode;
            this.Position = (source.Position ?? (object)"").ToString();
            this.Progress = source.Progress;
            this.LastCheckpoint = source.LastCheckpoint;
            this.EventsProcessedAfterRestart = source.EventsProcessedAfterRestart;
            this.BufferedEvents = source.BufferedEvents;
            this.CheckpointStatus = source.CheckpointStatus;
            this.WritePendingEventsBeforeCheckpoint = source.WritePendingEventsBeforeCheckpoint;
            this.WritePendingEventsAfterCheckpoint = source.WritePendingEventsAfterCheckpoint;
            this.ReadsInProgress = source.ReadsInProgress;
            this.WritesInProgress = source.WritesInProgress;
            this.CoreProcessingTime = source.CoreProcessingTime;
            this.PartitionsCached = source.PartitionsCached;
            var statusLocalUrl = "/projection/" + source.Name;
            this.StatusUrl = makeAbsoluteUrl(statusLocalUrl);
            this.StateUrl = makeAbsoluteUrl(statusLocalUrl + "/state");
            this.ResultUrl = makeAbsoluteUrl(statusLocalUrl + "/result");
            this.QueryUrl = makeAbsoluteUrl(statusLocalUrl + "/query?config=yes");
            if (source.Definition != null && !string.IsNullOrEmpty(source.Definition.ResultStreamName))
                this.ResultStreamUrl =
                    makeAbsoluteUrl("/streams/" + Uri.EscapeDataString(source.Definition.ResultStreamName));

            this.DisableCommandUrl = makeAbsoluteUrl(statusLocalUrl + "/command/disable");
            this.EnableCommandUrl = makeAbsoluteUrl(statusLocalUrl + "/command/enable");
        }

        public long CoreProcessingTime { get; set; }

        public int Version { get; set; }

        public int Epoch { get; set; }

        public string EffectiveName { get; set; }

        public int WritesInProgress { get; set; }

        public int ReadsInProgress { get; set; }

        public int PartitionsCached { get; set; }

        public string Status { get; set; }

        public string StateReason { get; set; }

        public string Name { get; set; }

        public ProjectionMode Mode { get; set; }

        public string Position { get; set; }

        public float Progress { get; set; }

        public string LastCheckpoint { get; set; }

        public int EventsProcessedAfterRestart { get; set; }

        public string StatusUrl { get; set; }

        public string StateUrl { get; set; }

        public string ResultUrl { get; set; }

        public string QueryUrl { get; set; }

        public string ResultStreamUrl { get; set; }

        public string EnableCommandUrl { get; set; }

        public string DisableCommandUrl { get; set; }

        public string CheckpointStatus { get; set; }

        public int BufferedEvents { get; set; }

        public int WritePendingEventsBeforeCheckpoint { get; set; }

        public int WritePendingEventsAfterCheckpoint { get; set; }
    }
}