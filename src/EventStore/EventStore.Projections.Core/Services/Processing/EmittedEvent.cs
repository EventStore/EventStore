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

namespace EventStore.Projections.Core.Services.Processing
{
    public abstract class EmittedEvent
    {
        public readonly string StreamId;
        public readonly Guid EventId;
        public readonly string EventType;
        private readonly CheckpointTag _causedByTag;
        private readonly CheckpointTag _expectedTag;
        private readonly Action<int> _onCommitted;
        private Guid _causedBy;
        private string _correlationId;

        //TODO: stream metadata
        protected EmittedEvent(
            string streamId, Guid eventId,
            string eventType, CheckpointTag causedByTag, CheckpointTag expectedTag, Action<int> onCommitted = null)
        {
            if (causedByTag == null) throw new ArgumentNullException("causedByTag");
            StreamId = streamId;
            EventId = eventId;
            EventType = eventType;
            _causedByTag = causedByTag;
            _expectedTag = expectedTag;
            _onCommitted = onCommitted;
        }

        public abstract string Data { get; }

        public CheckpointTag CausedByTag
        {
            get { return _causedByTag; }
        }

        public CheckpointTag ExpectedTag
        {
            get { return _expectedTag; }
        }

        public Action<int> OnCommitted
        {
            get { return _onCommitted; }
        }

        public Guid CausedBy
        {
            get { return _causedBy; }
        }

        public string CorrelationId
        {
            get { return _correlationId; }
        }

        public abstract bool IsJson { get; }

        public abstract bool IsReady();

        public virtual IEnumerable<KeyValuePair<string, string>> ExtraMetaData()
        {
            return null;
        }

        public void SetCausedBy(Guid causedBy)
        {
            _causedBy = causedBy;
        }

        public void SetCorrelationId(string correlationId)
        {
            _correlationId = correlationId;
        }
    }
}