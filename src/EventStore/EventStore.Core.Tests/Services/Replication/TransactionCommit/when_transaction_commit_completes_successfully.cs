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

using System.Collections.Generic;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.TransactionCommit
{
    [TestFixture]
    public class when_transaction_commit_completes_successfully : RequestManagerSpecification
    {
        protected override TwoPhaseRequestManagerBase OnManager(FakePublisher publisher)
        {
            return new TransactionCommitTwoPhaseRequestManager(publisher, 3, 3, PrepareTimeout, CommitTimeout);
        }

        protected override IEnumerable<Message> WithInitialMessages()
        {
            yield return new ClientMessage.TransactionCommit(InternalCorrId, ClientCorrId, Envelope, true, 4, null);
            yield return new StorageMessage.PrepareAck(InternalCorrId, 1, PrepareFlags.StreamDelete);
            yield return new StorageMessage.PrepareAck(InternalCorrId, 1, PrepareFlags.StreamDelete);
            yield return new StorageMessage.PrepareAck(InternalCorrId, 1, PrepareFlags.StreamDelete);
            yield return new StorageMessage.CommitAck(InternalCorrId, 100, 2, 3, 3);
            yield return new StorageMessage.CommitAck(InternalCorrId, 100, 2, 3, 3);

        }

        protected override Message When()
        {
            return new StorageMessage.CommitAck(InternalCorrId, 100, 2, 3, 3);
        }

        [Test]
        public void successful_request_message_is_publised()
        {
            Assert.That(Produced.ContainsSingle<StorageMessage.RequestCompleted>(
                x => x.CorrelationId == InternalCorrId && x.Success));
        }

        [Test]
        public void the_envelope_is_replied_to_with_success()
        {
            Assert.That(Envelope.Replies.ContainsSingle<ClientMessage.TransactionCommitCompleted>(
                x => x.CorrelationId == ClientCorrId && x.Result == OperationResult.Success));
        }
    }
}