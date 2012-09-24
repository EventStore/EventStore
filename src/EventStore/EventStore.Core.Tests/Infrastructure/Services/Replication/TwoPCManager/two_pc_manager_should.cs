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
    //TODO GFY REWRITE TESTS
    //[TestFixture]
    //public class two_pc_manager_should
    //{
    //    private TwoPhaseCommitRequestManager _manager;
    //    private FakePublisher _bus;
    //    private Guid _correlationID;
    //    private ReplicationMessage.CommitAck _commitAck;
    //    private ReplicationMessage.PrepareAck _prepareAck;

    //    [SetUp]
    //    public void SetUp()
    //    {
    //        _bus = new FakePublisher();
    //        _manager = new TwoPhaseCommitRequestManager(_bus, 2, 2);
    //        _correlationID = Guid.NewGuid();
    //        _manager.Handle(new ReplicationMessage.WriteRequestCreated(_correlationID,
    //                                                                   new NoopEnvelope(),
    //                                                                   "test-stream",
    //                                                                   -1,
    //                                                                   new Event[0]));
    //        _prepareAck = new ReplicationMessage.PrepareAck(_correlationID, 0, PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd);
    //        _commitAck = new ReplicationMessage.CommitAck(_correlationID, 0, 0);
    //    }

    //    [TearDown]
    //    public void TearDown()
    //    {
    //        _bus = null;
    //        _manager = null;
    //        _correlationID = Guid.Empty;
    //        _prepareAck = null;
    //        _commitAck = null;
    //    }

    //    [Test]
    //    public void not_accept_null_bus()
    //    {
    //        Assert.Throws<ArgumentNullException>(() => new TwoPhaseCommitRequestManager(null, 2, 2));
    //    }
    //    [Test]
    //    public void not_accept_zero_commit_count()
    //    {
    //        Assert.Throws<ArgumentOutOfRangeException>(() => new TwoPhaseCommitRequestManager(_bus, 2, 0));
    //    }
    //    [Test]
    //    public void not_accept_zero_prepare_count()
    //    {
    //        Assert.Throws<ArgumentOutOfRangeException>(() => new TwoPhaseCommitRequestManager(_bus, 0, 2));
    //    }

    //    [Test]
    //    public void ignore_prepare_timeout_after_switched_to_commit_phase()
    //    {
    //        _manager.Handle(_prepareAck);
    //        _manager.Handle(_prepareAck);

    //        _manager.Handle(new ReplicationMessage.PreparePhaseTimeout(_correlationID));

    //        Assert.That(_bus.Messages.ContainsNo<ReplicationMessage.RequestCompleted>(m => m.Success == false));
    //    }
    //    [Test]
    //    public void ignore_prepare_timeout_after_write_completed()
    //    {
    //        _manager.Handle(_prepareAck);
    //        _manager.Handle(_prepareAck);

    //        _manager.Handle(_commitAck);
    //        _manager.Handle(_commitAck);

    //        _manager.Handle(new ReplicationMessage.PreparePhaseTimeout(_correlationID));

    //        Assert.That(_bus.Messages.ContainsNo<ReplicationMessage.RequestCompleted>(m => m.Success == false));

    //    }
    //    [Test]
    //    public void ignore_commit_timeout_after_write_completed()
    //    {
    //        _manager.Handle(_prepareAck);
    //        _manager.Handle(_prepareAck);

    //        _manager.Handle(_commitAck);
    //        _manager.Handle(_commitAck);

    //        _manager.Handle(new ReplicationMessage.CommitPhaseTimeout(_correlationID));

    //        Assert.That(_bus.Messages.ContainsNo<ReplicationMessage.RequestCompleted>(m => m.Success == false));
    //    }

    //    [Test]
    //    public void fail_transaction_if_not_enough_prepare_acks_received_and_prepare_timeout_triggered()
    //    {
    //        _manager.Handle(_prepareAck);
    //        _manager.Handle(new ReplicationMessage.PreparePhaseTimeout(_correlationID));

    //        Assert.That(_bus.Messages.ContainsSingle<ReplicationMessage.RequestCompleted>(m => m.Success == false));
    //    }
    //    [Test]
    //    public void fail_transaction_if_switched_to_commit_phase_but_not_enough_commit_acks_received_and_commit_timeout_triggered()
    //    {
    //        _manager.Handle(_prepareAck);
    //        _manager.Handle(_prepareAck);

    //        _manager.Handle(_commitAck);
    //        _manager.Handle(new ReplicationMessage.CommitPhaseTimeout(_correlationID));

    //        Assert.That(_bus.Messages.ContainsSingle<ReplicationMessage.RequestCompleted>(m => m.Success == false));
    //    }
    //    [Test]
    //    public void end_transaction_successfully_when_enough_prepare_and_commit_acks_received_in_time()
    //    {
    //        _manager.Handle(_prepareAck);
    //        _manager.Handle(_prepareAck);

    //        _manager.Handle(_commitAck);
    //        _manager.Handle(_commitAck);

    //        Assert.That(_bus.Messages.ContainsSingle<ReplicationMessage.RequestCompleted>(m => m.Success == true));
    //    }

    //    [Test]
    //    public void fail_transaction_if_wrong_expected_version_received()
    //    {
    //        _manager.Handle(new ReplicationMessage.WrongExpectedVersion(_correlationID));

    //        Assert.That(_bus.Messages.ContainsSingle<ReplicationMessage.RequestCompleted>(m => m.Success == false));
    //    }
    //    [Test]
    //    public void should_ignore_prepare_and_commit_acks_after_wrong_expected_version_received()
    //    {
    //        _manager.Handle(new ReplicationMessage.WrongExpectedVersion(_correlationID));

    //        _manager.Handle(_prepareAck);
    //        _manager.Handle(_prepareAck);

    //        _manager.Handle(_commitAck);
    //        _manager.Handle(_commitAck);

    //        Assert.That(_bus.Messages.ContainsNo<ReplicationMessage.RequestCompleted>(m => m.Success == true));
    //    }
    //    [Test]
    //    public void should_ignore_prepare_and_commit_acks_after_prepare_timeout_triggered()
    //    {
    //        _manager.Handle(new ReplicationMessage.PreparePhaseTimeout(_correlationID));

    //        _manager.Handle(_prepareAck);
    //        _manager.Handle(_prepareAck);

    //        _manager.Handle(_commitAck);
    //        _manager.Handle(_commitAck);

    //        Assert.That(_bus.Messages.ContainsNo<ReplicationMessage.RequestCompleted>(m => m.Success == true));
    //    }
    //    [Test]
    //    public void should_ignore_prepare_and_commit_acks_after_commit_timeout_triggered()
    //    {
    //        _manager.Handle(new ReplicationMessage.CommitPhaseTimeout(_correlationID));

    //        _manager.Handle(_prepareAck);
    //        _manager.Handle(_prepareAck);

    //        _manager.Handle(_commitAck);
    //        _manager.Handle(_commitAck);

    //        Assert.That(_bus.Messages.ContainsNo<ReplicationMessage.RequestCompleted>(m => m.Success == true));
    //    }
    //}
}
