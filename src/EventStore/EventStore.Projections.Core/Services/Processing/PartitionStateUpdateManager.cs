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
using System.Linq;

namespace EventStore.Projections.Core.Services.Processing
{
    public class PartitionStateUpdateManager
    {
        private class State
        {
            public PartitionState PartitionState;
            public CheckpointTag ExpectedTag;
        }

        private readonly Dictionary<string, State> _states = new Dictionary<string, State>();
        private readonly ProjectionNamesBuilder _namingBuilder;

        public PartitionStateUpdateManager(ProjectionNamesBuilder namingBuilder)
        {
            if (namingBuilder == null) throw new ArgumentNullException("namingBuilder");
            _namingBuilder = namingBuilder;
        }

        public void StateUpdated(string partition, PartitionState state, CheckpointTag basedOn)
        {
            State stateEntry;
            if (_states.TryGetValue(partition, out stateEntry))
            {
                stateEntry.PartitionState = state;
            }
            else
            {
                _states.Add(partition, new State { PartitionState = state, ExpectedTag = basedOn});
            }
        }

        public void EmitEvents(IEventWriter eventWriter)
        {
            if (_states.Count > 0)
            {
                var list = new List<EmittedEvent>();
                foreach (var entry in _states)
                {
                    var partition = entry.Key;
                    var streamId = _namingBuilder.MakePartitionCheckpointStreamName(partition);
                    var data = entry.Value.PartitionState.Serialize();
                    var causedBy = entry.Value.PartitionState.CausedBy;
                    var expectedTag = entry.Value.ExpectedTag;
                    list.Add(new EmittedDataEvent(streamId, Guid.NewGuid(), "Checkpoint", data, causedBy, expectedTag));
                }
                //NOTE: order yb is required to satisfy internal emit events validation
                // which ensures that events are ordered by causedBy tag.  
                // it is too strong check, but ...
                eventWriter.ValidateOrderAndEmitEvents(list.OrderBy(v => v.CausedByTag).ToArray());
            }
        }
    }
}
    