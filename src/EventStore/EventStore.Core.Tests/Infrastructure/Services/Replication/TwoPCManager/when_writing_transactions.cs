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
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Tests.Common;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Infrastructure.Services.Replication.TwoPCManager
{
    [TestFixture]
    public class when_writing_transaction
    {
        private const int _importlessLogPosition = 0; // for commits - they are currently not used anywhere
        private const int _startingLogPosition = 0;

        private TwoPhaseCommitRequestManager _manager;
        private FakePublisher _bus;
        private Guid _correlationID;

        [SetUp]
        public void SetUp()
        {
            _bus = new FakePublisher();
            _manager = new TwoPhaseCommitRequestManager(_bus, 2, 2);

            _correlationID = Guid.NewGuid();

            _manager.Handle(new ReplicationMessage.WriteRequestCreated(
                                _correlationID,
                                new NoopEnvelope(),
                                "test-stream",
                                -1,
                                new[]
                                {
                                    new Event(Guid.NewGuid(), "test-event-type", false,  null, null),
                                    new Event(Guid.NewGuid(), "test-event-type", false,  null, null),
                                    new Event(Guid.NewGuid(), "test-event-type", false,  null, null)
                                }));
        }

        [TearDown]
        public void TearDown()
        {
            _bus = null;
            _manager = null;
            _correlationID = Guid.Empty;
        }

        [Test]
        public void should_succeed_if_all_prepare_and_commit_acks_was_received()
        {
            _manager.Handle(new ReplicationMessage.PrepareAck(_correlationID, _startingLogPosition, PrepareFlags.TransactionBegin));
            _manager.Handle(new ReplicationMessage.PrepareAck(_correlationID, _startingLogPosition + 1, PrepareFlags.None));
            _manager.Handle(new ReplicationMessage.PrepareAck(_correlationID, _startingLogPosition + 2, PrepareFlags.TransactionEnd));

            _manager.Handle(new ReplicationMessage.PrepareAck(_correlationID, _startingLogPosition, PrepareFlags.TransactionBegin));
            _manager.Handle(new ReplicationMessage.PrepareAck(_correlationID, _startingLogPosition + 1, PrepareFlags.None));
            _manager.Handle(new ReplicationMessage.PrepareAck(_correlationID, _startingLogPosition + 2, PrepareFlags.TransactionEnd));

            _manager.Handle(new ReplicationMessage.CommitAck(_correlationID, _startingLogPosition, _startingLogPosition));
            _manager.Handle(new ReplicationMessage.CommitAck(_correlationID, _startingLogPosition, _startingLogPosition));

            Assert.That(_bus.Messages.ContainsSingle<ReplicationMessage.RequestCompleted>(m => m.Success == true));
        }

        [Test]
        public void should_succeed_if_middle_prepare_acks_was_missed_and_all_commit_acks_was_received()
        {
            _manager.Handle(new ReplicationMessage.PrepareAck(_correlationID, _startingLogPosition, PrepareFlags.TransactionBegin));
            _manager.Handle(new ReplicationMessage.PrepareAck(_correlationID, _startingLogPosition + 2, PrepareFlags.TransactionEnd));

            _manager.Handle(new ReplicationMessage.PrepareAck(_correlationID, _startingLogPosition, PrepareFlags.TransactionBegin));
            _manager.Handle(new ReplicationMessage.PrepareAck(_correlationID, _startingLogPosition + 2, PrepareFlags.TransactionEnd));

            _manager.Handle(new ReplicationMessage.CommitAck(_correlationID, _startingLogPosition, _startingLogPosition));
            _manager.Handle(new ReplicationMessage.CommitAck(_correlationID, _startingLogPosition, _startingLogPosition));

            Assert.That(_bus.Messages.ContainsSingle<ReplicationMessage.RequestCompleted>(m => m.Success == true));
        }

        [Test]
        public void should_fail_if_last_prepare_acks_was_missed_and_prepare_timeout_triggerred()
        {
            _manager.Handle(new ReplicationMessage.PrepareAck(_correlationID, _startingLogPosition, PrepareFlags.TransactionBegin));
            _manager.Handle(new ReplicationMessage.PrepareAck(_correlationID, _startingLogPosition + 1, PrepareFlags.None));

            _manager.Handle(new ReplicationMessage.PrepareAck(_correlationID, _startingLogPosition, PrepareFlags.TransactionBegin));
            _manager.Handle(new ReplicationMessage.PrepareAck(_correlationID, _startingLogPosition + 1, PrepareFlags.None));

            _manager.Handle(new ReplicationMessage.PreparePhaseTimeout(_correlationID));

            Assert.That(_bus.Messages.ContainsSingle<ReplicationMessage.RequestCompleted>(m => m.Success == false));
        }

        [Test]
        public void should_succeed_if_first_prepare_acks_was_missed_and_all_commit_acks_was_received()
        {
            _manager.Handle(new ReplicationMessage.PrepareAck(_correlationID, _startingLogPosition, PrepareFlags.None));
            _manager.Handle(new ReplicationMessage.PrepareAck(_correlationID, _startingLogPosition + 1, PrepareFlags.TransactionEnd));

            _manager.Handle(new ReplicationMessage.PrepareAck(_correlationID, _startingLogPosition, PrepareFlags.None));
            _manager.Handle(new ReplicationMessage.PrepareAck(_correlationID, _startingLogPosition + 1, PrepareFlags.TransactionEnd));

            _manager.Handle(new ReplicationMessage.CommitAck(_correlationID, _importlessLogPosition, _importlessLogPosition));
            _manager.Handle(new ReplicationMessage.CommitAck(_correlationID, _importlessLogPosition, _importlessLogPosition));

            Assert.That(_bus.Messages.ContainsSingle<ReplicationMessage.RequestCompleted>(m => m.Success == true));
        }
    }
}
