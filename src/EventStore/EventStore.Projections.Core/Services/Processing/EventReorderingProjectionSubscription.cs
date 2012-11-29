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
using System.Collections.Generic;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;
using System.Linq;

namespace EventStore.Projections.Core.Services.Processing
{
    public class EventReorderingProjectionSubscription : ProjectionSubscriptionBase, IProjectionSubscription
    {
        private readonly SortedList<long, ProjectionCoreServiceMessage.CommittedEventDistributed> _buffer =
            new SortedList<long, ProjectionCoreServiceMessage.CommittedEventDistributed>();

        private readonly int _processingLagMs;

        public EventReorderingProjectionSubscription(
            Guid projectionCorrelationId, CheckpointTag from,
            IHandle<ProjectionSubscriptionMessage.CommittedEventReceived> eventHandler,
            IHandle<ProjectionSubscriptionMessage.CheckpointSuggested> checkpointHandler,
            IHandle<ProjectionSubscriptionMessage.ProgressChanged> progressHandler,
            CheckpointStrategy checkpointStrategy, long? checkpointUnhandledBytesThreshold, int processingLagMs)
            : base(projectionCorrelationId, @from, eventHandler, checkpointHandler, progressHandler, checkpointStrategy, checkpointUnhandledBytesThreshold)
        {
            _processingLagMs = processingLagMs;
        }

        public void Handle(ProjectionCoreServiceMessage.CommittedEventDistributed message)
        {
            if (message.Data == null)
                throw new NotSupportedException();
            ProjectionCoreServiceMessage.CommittedEventDistributed existing;
            // ignore duplicate messages (when replaying from heading event distribution point)
            if (!_buffer.TryGetValue(message.Position.PreparePosition, out existing))
            {
                _buffer.Add(message.Position.PreparePosition, message);
                var maxTimestamp = _buffer.Max(v => v.Value.Data.Timestamp);
                ProcessAllFor(maxTimestamp);
            }
        }

        private void ProcessAllFor(DateTime maxTimestamp)
        {

            if (_buffer.Count == 0)
                throw new InvalidOperationException();

            //NOTE: this is the most straightforward implementation 
            //TODO: build proper data structure when the approach is finalized
            bool processed;
            do
            {
                processed = ProcessFor(maxTimestamp);
            } while (processed);
        }

        private bool ProcessFor(DateTime maxTimestamp)
        {
            if (_buffer.Count == 0)
                return false;
            var first = _buffer.ElementAt(0);
            if ((maxTimestamp - first.Value.Data.Timestamp).TotalMilliseconds > _processingLagMs)
            {
                _buffer.RemoveAt(0);
                ProcessOne(first.Value);
                return true;
            }
            return false;
        }

        public void Handle(ProjectionCoreServiceMessage.EventDistributionPointIdle message)
        {
            ProcessAllFor(message.IdleTimestampUtc);
        }
    }
}
