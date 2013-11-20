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
using EventStore.Core.Helpers;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Tests.Services.event_reader.heading_event_reader
{
    class FakeReaderSubscription : IReaderSubscription
    {
        private readonly List<ReaderSubscriptionMessage.CommittedEventDistributed> _receivedEvents =
            new List<ReaderSubscriptionMessage.CommittedEventDistributed>();

        private readonly List<ReaderSubscriptionMessage.EventReaderIdle> _receivedIdleNotifications =
            new List<ReaderSubscriptionMessage.EventReaderIdle>();

        private readonly List<ReaderSubscriptionMessage.EventReaderStarting> _receivedStartingNotifications =
            new List<ReaderSubscriptionMessage.EventReaderStarting>();

        private readonly List<ReaderSubscriptionMessage.EventReaderEof> _receivedEofNotifications =
            new List<ReaderSubscriptionMessage.EventReaderEof>();

        private readonly List<ReaderSubscriptionMessage.EventReaderPartitionEof> _receivedPartitionEofNotifications =
            new List<ReaderSubscriptionMessage.EventReaderPartitionEof>();

        private readonly List<ReaderSubscriptionMessage.EventReaderPartitionMeasured> _receivedPartitionMeasuredNotifications =
            new List<ReaderSubscriptionMessage.EventReaderPartitionMeasured>();

        private readonly List<ReaderSubscriptionMessage.EventReaderNotAuthorized> _receivedNotAuthorizedNotifications =
            new List<ReaderSubscriptionMessage.EventReaderNotAuthorized>();

        public void Handle(ReaderSubscriptionMessage.CommittedEventDistributed message)
        {
            _receivedEvents.Add(message);
        }

        public List<ReaderSubscriptionMessage.CommittedEventDistributed> ReceivedEvents
        {
            get { return _receivedEvents; }
        }

        public List<ReaderSubscriptionMessage.EventReaderIdle> ReceivedIdleNotifications
        {
            get { return _receivedIdleNotifications; }
        }

        public List<ReaderSubscriptionMessage.EventReaderStarting> ReceivedStartingNotifications
        {
            get { return _receivedStartingNotifications; }
        }

        public List<ReaderSubscriptionMessage.EventReaderEof> ReceivedEofNotifications
        {
            get { return _receivedEofNotifications; }
        }

        public List<ReaderSubscriptionMessage.EventReaderPartitionEof> ReceivedPartitionEofNotifications
        {
            get { return _receivedPartitionEofNotifications; }
        }

        public List<ReaderSubscriptionMessage.EventReaderNotAuthorized> ReceivedNotAuthorizedNotifications
        {
            get { return _receivedNotAuthorizedNotifications; }
        }

        public void Handle(ReaderSubscriptionMessage.EventReaderIdle message)
        {
            _receivedIdleNotifications.Add(message);
        }

        public void Handle(ReaderSubscriptionMessage.EventReaderStarting message)
        {
            _receivedStartingNotifications.Add(message);
        }

        public void Handle(ReaderSubscriptionMessage.EventReaderEof message)
        {
            _receivedEofNotifications.Add(message);
        }

        public void Handle(ReaderSubscriptionMessage.EventReaderPartitionEof message)
        {
            _receivedPartitionEofNotifications.Add(message);
        }

        public void Handle(ReaderSubscriptionMessage.EventReaderPartitionMeasured message)
        {
            _receivedPartitionMeasuredNotifications.Add(message);
        }

        public void Handle(ReaderSubscriptionMessage.EventReaderNotAuthorized message)
        {
            _receivedNotAuthorizedNotifications.Add(message);
        }

        public IEventReader CreatePausedEventReader(
            IPublisher publisher, IODispatcher ioDispatcher, Guid forkedEventReaderId)
        {
            throw new NotImplementedException();
        }
    }
}
