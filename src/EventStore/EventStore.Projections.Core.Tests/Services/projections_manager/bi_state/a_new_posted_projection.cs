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
using System.Text;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.bi_state
{
    public static class a_new_posted_projection
    {
        public abstract class Base : TestFixtureWithProjectionCoreAndManagementServices
        {
            protected string _projectionName;
            protected string _projectionSource;
            protected Type _fakeProjectionType;
            protected ProjectionMode _projectionMode;
            protected bool _checkpointsEnabled;
            protected bool _emitEnabled;

            protected override void Given()
            {
                base.Given();
                AllWritesSucceed();
                NoOtherStreams();

                _projectionName = "test-projection";
                _projectionSource = @"";
                _fakeProjectionType = typeof (FakeBiStateProjection);
                _projectionMode = ProjectionMode.Continuous;
                _checkpointsEnabled = true;
                _emitEnabled = false;
            }

            protected override IEnumerable<WhenStep> When()
            {
                yield return(new SystemMessage.BecomeMaster(Guid.NewGuid()));
                yield return
                    (new ProjectionManagementMessage.Post(
                        new PublishEnvelope(_bus), _projectionMode, _projectionName,
                        ProjectionManagementMessage.RunAs.System, "native:" + _fakeProjectionType.AssemblyQualifiedName,
                        _projectionSource, enabled: true, checkpointsEnabled: _checkpointsEnabled,
                        emitEnabled: _emitEnabled));
            }
        }

        [TestFixture]
        public class when_get_state : Base
        {
            protected override IEnumerable<WhenStep> When()
            {
                foreach (var m in base.When()) yield return m;
                yield return(
                    new ProjectionManagementMessage.GetState(new PublishEnvelope(_bus), _projectionName, ""));
            }

            [Test]
            public void returns_correct_state()
            {
                Assert.AreEqual(
                    1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().Count());
                Assert.AreEqual(
                    _projectionName,
                    _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().Single().Name);
                Assert.AreEqual(
                    "", _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().Single().State);
            }
        }

        [TestFixture]
        public class when_stopping : Base
        {
            private Guid _reader;

            protected override IEnumerable<WhenStep> When()
            {
                foreach (var m in base.When()) yield return m;

                var readerAssignedMessage =
                    _consumer.HandledMessages.OfType<EventReaderSubscriptionMessage.ReaderAssignedReader>().LastOrDefault();
                Assert.IsNotNull(readerAssignedMessage);
                _reader = readerAssignedMessage.ReaderId;

                yield return
                    (ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
                        _reader, new TFPos(100, 50), new TFPos(100, 50), "stream1", 1, "stream1", 1, false, Guid.NewGuid(),
                        "type", false, Helper.UTF8NoBom.GetBytes("1"), new byte[0], 100, 33.3f));

                yield return
                    (ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
                        _reader, new TFPos(200, 150), new TFPos(200, 150), "stream2", 1, "stream2", 1, false, Guid.NewGuid(),
                        "type", false, Helper.UTF8NoBom.GetBytes("1"), new byte[0], 100, 33.3f));

                yield return
                    new ProjectionManagementMessage.Disable(
                        new PublishEnvelope(_bus), _projectionName, ProjectionManagementMessage.RunAs.System);
            }

            [Test]
            public void writes_both_stream_and_shared_partition_checkpoints()
            {
                var writeProjectionCheckpoints =
                    HandledMessages.OfType<ClientMessage.WriteEvents>().OfEventType("$ProjectionCheckpoint").ToArray();
                var writeCheckpoints =
                    HandledMessages.OfType<ClientMessage.WriteEvents>().OfEventType("$Checkpoint").ToArray();

                Assert.AreEqual(1, writeProjectionCheckpoints.Length);
                Assert.AreEqual(@"[{""data"": 2}]", Encoding.UTF8.GetString(writeProjectionCheckpoints[0].Data));
                Assert.AreEqual(2, writeCheckpoints.Length);

                Assert.That(
                    writeCheckpoints.All(
                        v => Encoding.UTF8.GetString(v.Data) == @"[{""data"": 1},{""data"": 1}]"));
            }
        }
    }
}
