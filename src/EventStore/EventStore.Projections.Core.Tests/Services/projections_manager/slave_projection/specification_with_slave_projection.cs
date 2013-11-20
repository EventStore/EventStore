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
using EventStore.Core.Messages;
using EventStore.Core.Services.UserManagement;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.slave_projection
{
    public abstract class specification_with_slave_projection : TestFixtureWithProjectionCoreAndManagementServices
    {
        protected Guid _coreProjectionCorrelationId;

        protected override void Given()
        {
            base.Given();
            _coreProjectionCorrelationId = Guid.NewGuid();
            AllWritesSucceed();
            NoOtherStreams();
        }

        protected override IEnumerable<WhenStep> When()
        {
            yield return new SystemMessage.BecomeMaster(Guid.NewGuid());
            yield return
                new CoreProjectionManagementMessage.CreateAndPrepareSlave(
                    Envelope, _coreProjectionCorrelationId, "projection", new ProjectionVersion(1, 0, 0),
                    new ProjectionConfig(
                        SystemAccount.Principal, 0, 0, 1000, 1000, false, false, false, true, isSlaveProjection: true),
                    GetInputQueue(), _coreProjectionCorrelationId, () => new FakeProjectionStateHandler(
                        configureBuilder: builder =>
                        {
                            builder.FromCatalogStream("catalog");
                            builder.AllEvents();
                            builder.SetByStream();
                            builder.SetLimitingCommitPosition(10000);
                        }));
            yield return Yield;
        }
    }

    [TestFixture]
    public class when_creating_a_slave_projection : specification_with_slave_projection
    {
        protected override IEnumerable<WhenStep> When()
        {
            foreach (var m in base.When()) yield return m;
            yield return new CoreProjectionManagementMessage.Start(_coreProjectionCorrelationId);
        }

        [Test]
        public void replies_with_slave_projection_reader_id_on_started_message()
        {
            var readerAssigned =
                HandledMessages.OfType<CoreProjectionManagementMessage.SlaveProjectionReaderAssigned>().LastOrDefault();

            Assert.IsNotNull(readerAssigned);
            Assert.AreNotEqual(Guid.Empty, readerAssigned.SubscriptionId);
        }
    }

    [TestFixture]
    public class when_processing_a_stream : specification_with_slave_projection
    {
        private Guid _subscriptionId;

        protected override void Given()
        {
            base.Given();
            ExistingEvent("test-stream", "handle_this_type", "", "{\"data\":1}");
        }

        protected override IEnumerable<WhenStep> When()
        {
            foreach (var m in base.When()) yield return m;
            yield return new CoreProjectionManagementMessage.Start(_coreProjectionCorrelationId);
            var readerAssigned =
                HandledMessages.OfType<CoreProjectionManagementMessage.SlaveProjectionReaderAssigned>().LastOrDefault();
            Assert.IsNotNull(readerAssigned);
            _subscriptionId = readerAssigned.SubscriptionId;
            yield return
                new ReaderSubscriptionManagement.SpoolStreamReading(_subscriptionId, Guid.NewGuid(), "test-stream", 0, 10000);
        }

        [Test]
        public void publishes_partition_result_message()
        {
            var results =
                HandledMessages.OfType<PartitionProcessingResult>().ToArray();
            Assert.AreEqual(1, results.Length);
            var result = results[0];
            Assert.AreEqual("{\"data\":1}", result.Result);
        }
    }

    [TestFixture]
    public class when_processing_a_non_existing_stream : specification_with_slave_projection
    {
        private Guid _subscriptionId;

        protected override void Given()
        {
            base.Given();
            // No streams
        }

        protected override IEnumerable<WhenStep> When()
        {
            foreach (var m in base.When()) yield return m;
            yield return new CoreProjectionManagementMessage.Start(_coreProjectionCorrelationId);
            var readerAssigned =
                HandledMessages.OfType<CoreProjectionManagementMessage.SlaveProjectionReaderAssigned>().LastOrDefault();
            Assert.IsNotNull(readerAssigned);
            _subscriptionId = readerAssigned.SubscriptionId;
            yield return
                new ReaderSubscriptionManagement.SpoolStreamReading(_subscriptionId, Guid.NewGuid(), "test-stream", 0, 10000);
        }

        [Test]
        public void publishes_partition_result_message()
        {
            var results =
                HandledMessages.OfType<PartitionProcessingResult>().ToArray();
            Assert.AreEqual(1, results.Length);
            var result = results[0];
            Assert.IsNull(result.Result);
        }
    }

    [TestFixture]
    public class when_processing_multiple_streams : specification_with_slave_projection
    {
        private Guid _subscriptionId;

        protected override void Given()
        {
            base.Given();
            ExistingEvent("test-stream", "handle_this_type", "", "{\"data\":1}");
            ExistingEvent("test-stream", "handle_this_type", "", "{\"data\":1}");
            ExistingEvent("test-stream2", "handle_this_type", "", "{\"data\":2}");
        }

        protected override IEnumerable<WhenStep> When()
        {
            foreach (var m in base.When()) yield return m;
            yield return new CoreProjectionManagementMessage.Start(_coreProjectionCorrelationId);
            var readerAssigned =
                HandledMessages.OfType<CoreProjectionManagementMessage.SlaveProjectionReaderAssigned>().LastOrDefault();
            Assert.IsNotNull(readerAssigned);
            _subscriptionId = readerAssigned.SubscriptionId;
            yield return
                new ReaderSubscriptionManagement.SpoolStreamReading(_subscriptionId, Guid.NewGuid(), "test-stream", 0, 10000);
            yield return
                new ReaderSubscriptionManagement.SpoolStreamReading(_subscriptionId, Guid.NewGuid(), "test-stream2", 1, 10000);
            yield return
                new ReaderSubscriptionManagement.SpoolStreamReading(_subscriptionId, Guid.NewGuid(), "test-stream3", 2, 10000);
        }

        [Test]
        public void publishes_partition_result_message()
        {
            var results =
                HandledMessages.OfType<PartitionProcessingResult>().ToArray();
            Assert.AreEqual(3, results.Length);
            Assert.AreEqual("{\"data\":1}", results[0].Result);
            Assert.AreEqual("{\"data\":2}", results[1].Result);
            Assert.IsNull(results[2].Result);
        }
    }
}
