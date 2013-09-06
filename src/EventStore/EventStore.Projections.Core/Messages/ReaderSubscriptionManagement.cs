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
    public static class ReaderSubscriptionManagement
    {
        public abstract class ReaderSubscriptionManagementMessage : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly Guid _subscriptionId;

            protected ReaderSubscriptionManagementMessage(Guid subscriptionId)
            {
                _subscriptionId = subscriptionId;
            }

            public Guid SubscriptionId
            {
                get { return _subscriptionId; }
            }
        }

        public class Subscribe : ReaderSubscriptionManagementMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly CheckpointTag _fromPosition;
            private readonly IReaderStrategy _readerStrategy;
            private readonly ReaderSubscriptionOptions _options;

            public Subscribe(
                Guid subscriptionId, CheckpointTag from,
                IReaderStrategy readerStrategy, ReaderSubscriptionOptions readerSubscriptionOptions): base(subscriptionId)
            {
                if (@from == null) throw new ArgumentNullException("from");
                if (readerStrategy == null) throw new ArgumentNullException("readerStrategy");
                _fromPosition = @from;
                _readerStrategy = readerStrategy;
                _options = readerSubscriptionOptions;
            }

            public CheckpointTag FromPosition
            {
                get { return _fromPosition; }
            }

            public IReaderStrategy ReaderStrategy
            {
                get { return _readerStrategy; }
            }

            public ReaderSubscriptionOptions Options
            {
                get { return _options; }
            }
        }

        public class Pause : ReaderSubscriptionManagementMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public Pause(Guid subscriptionId)
                : base(subscriptionId)
            {
            }

        }

        public class Resume : ReaderSubscriptionManagementMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public Resume(Guid subscriptionId)
                : base(subscriptionId)
            {
            }

        }

        public class Unsubscribe : ReaderSubscriptionManagementMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public Unsubscribe(Guid subscriptionId)
                : base(subscriptionId)
            {
            }

        }

        public class ReaderAssignedReader : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly Guid _correlationId;
            private readonly Guid _readerId;

            public ReaderAssignedReader(Guid correlationId, Guid readerId)
            {
                _correlationId = correlationId;
                _readerId = readerId;
            }

            public Guid CorrelationId
            {
                get { return _correlationId; }
            }

            public Guid ReaderId
            {
                get { return _readerId; }
            }
        }

        public sealed class SpoolStreamReading : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Guid CorrelationId;
            public readonly string StreamId;
            public readonly int CatalogSequenceNumber;

            public SpoolStreamReading(Guid correlationId, string streamId, int catalogSequenceNumber)
            {
                CorrelationId = correlationId;
                StreamId = streamId;
                CatalogSequenceNumber = catalogSequenceNumber;
            }
        }

    }
}
