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
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Services
{
    public class ProjectionStatistics
    {
        //TODO: resolve name collisions...

        public string Status { get; set; }

        public bool Enabled { get; set; }

        public ManagedProjectionState MasterStatus { get; set; }

        public string StateReason { get; set; }

        public string Name { get; set; }

        public int Epoch { get; set; }

        public int Version { get; set; }

        public ProjectionMode Mode { get; set; }

        public CheckpointTag Position { get; set; }

        public float Progress { get; set; }

        public string LastCheckpoint { get; set; }

        public int EventsProcessedAfterRestart { get; set; }

        public int BufferedEvents { get; set; }

        public string CheckpointStatus { get; set; }

        public int WritePendingEventsBeforeCheckpoint { get; set; }

        public int WritePendingEventsAfterCheckpoint { get; set; }

        public int PartitionsCached { get; set; }

        public int ReadsInProgress { get; set; }

        public int WritesInProgress { get; set; }

        public string EffectiveName { get; set; }

        public ProjectionStatistics Clone()
        {
            return (ProjectionStatistics) MemberwiseClone();
        }
    }
}