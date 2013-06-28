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
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Messages
{
    public abstract class EventReaderSubscriptionMessage : Message
    {
        private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId { get { return TypeId; } }

        private readonly Guid _subscriptionId;
        private readonly long _subscriptionMessageSequenceNumber;
        private readonly object _source;
        private readonly CheckpointTag _checkpointTag;
        private readonly float _progress;

        private EventReaderSubscriptionMessage(Guid subscriptionId, CheckpointTag checkpointTag, float progress, long subscriptionMessageSequenceNumber, object source)
        {
            _subscriptionId = subscriptionId;
            _checkpointTag = checkpointTag;
            _progress = progress;
            _subscriptionMessageSequenceNumber = subscriptionMessageSequenceNumber;
            _source = source;
        }

        /// <summary>
        /// A CheckpointSuggested message is sent to core projection 
        /// to allow bookmarking a position that can be used to 
        /// restore the projection processing (typically
        /// an event at this position does not satisfy projection filter)
        /// </summary>
        public class CheckpointSuggested : EventReaderSubscriptionMessage
        {
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public CheckpointSuggested(
                Guid subscriptionId, CheckpointTag checkpointTag, float progress,
                long subscriptionMessageSequenceNumber, object source = null)
                : base(subscriptionId, checkpointTag, progress, subscriptionMessageSequenceNumber, source)
            {
            }
        }

        public class ProgressChanged : EventReaderSubscriptionMessage
        {
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public ProgressChanged(
                Guid subscriptionId, CheckpointTag checkpointTag, float progress,
                long subscriptionMessageSequenceNumber, object source = null)
                : base(subscriptionId, checkpointTag, progress, subscriptionMessageSequenceNumber, source)
            {
            }
        }

        public sealed class NotAuthorized : EventReaderSubscriptionMessage
        {
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public NotAuthorized(
                Guid subscriptionId, CheckpointTag checkpointTag, float progress, long subscriptionMessageSequenceNumber,
                object source = null)
                : base(subscriptionId, checkpointTag, progress, subscriptionMessageSequenceNumber, source)
            {
            }
        }

        public class EofReached : EventReaderSubscriptionMessage
        {
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public EofReached(
                Guid subscriptionId, CheckpointTag checkpointTag,
                long subscriptionMessageSequenceNumber, object source = null)
                : base(subscriptionId, checkpointTag, 100.0f, subscriptionMessageSequenceNumber, source)
            {
            }
        }

        public class CommittedEventReceived : EventReaderSubscriptionMessage
        {
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public static CommittedEventReceived Sample(
                ResolvedEvent data, Guid subscriptionId, long subscriptionMessageSequenceNumber)
            {
                return new CommittedEventReceived(
                    subscriptionId, null, data, 77.7f, subscriptionMessageSequenceNumber);
            }

            private readonly ResolvedEvent _data;

            private readonly string _eventCategory;

            private CommittedEventReceived(Guid subscriptionId, CheckpointTag checkpointTag, string eventCategory, ResolvedEvent data, float progress, long subscriptionMessageSequenceNumber, object source)
                : base(subscriptionId, checkpointTag, progress, subscriptionMessageSequenceNumber, source)
            {
                if (data == null) throw new ArgumentNullException("data");
                _data = data;
                _eventCategory = eventCategory;
            }

            private CommittedEventReceived(
                Guid subscriptionId, string eventCategory, ResolvedEvent data, float progress,
                long subscriptionMessageSequenceNumber)
                : this(
                    subscriptionId,
                    CheckpointTag.FromPosition(data.Position.CommitPosition, data.Position.PreparePosition),
                    eventCategory, data, progress, subscriptionMessageSequenceNumber, null)
            {
            }

            public ResolvedEvent Data
            {
                get { return _data; }
            }

            public string EventCategory
            {
                get { return _eventCategory; }
            }

            public static CommittedEventReceived FromCommittedEventDistributed(
                ReaderSubscriptionMessage.CommittedEventDistributed message, CheckpointTag checkpointTag,
                string eventCategory, Guid subscriptionId, long subscriptionMessageSequenceNumber)
            {
                return new CommittedEventReceived(
                    subscriptionId, checkpointTag, eventCategory, message.Data, message.Progress,
                    subscriptionMessageSequenceNumber, message.Source);
            }
        }

        public CheckpointTag CheckpointTag
        {
            get { return _checkpointTag; }
        }

        public float Progress
        {
            get { return _progress; }
        }

        public long SubscriptionMessageSequenceNumber
        {
            get { return _subscriptionMessageSequenceNumber; }
        }

        public Guid SubscriptionId
        {
            get { return _subscriptionId; }
        }

        public object Source
        {
            get { return _source; }
        }
    }
}
