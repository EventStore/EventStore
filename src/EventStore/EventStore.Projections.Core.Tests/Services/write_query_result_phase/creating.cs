﻿// Copyright (c) 2012, Event Store LLP
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
using System.Linq;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection.checkpoint_manager;
using EventStore.Projections.Core.Tests.Services.core_projection.multi_phase;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.write_query_result_phase
{
    namespace creating
    {

        [TestFixture]
        class when_creating
        {
            [Test]
            public void it_can_be_created()
            {
                var coreProjection = new FakeCoreProjection();
                var stateCache = new PartitionStateCache();
                var bus = new InMemoryBus("test");
                var fakeCheckpointManager = new specification_with_multi_phase_core_projection.FakeCheckpointManager(bus, Guid.NewGuid());
                var it = new WriteQueryResultProjectionProcessingPhase(
                    1, "result-stream", coreProjection, stateCache,
                    fakeCheckpointManager, fakeCheckpointManager);
            }
        }

        abstract class specification_with_write_query_result_projection_processing_phase
        {
            protected WriteQueryResultProjectionProcessingPhase _phase;
            protected specification_with_multi_phase_core_projection.FakeCheckpointManager _checkpointManager;
            protected InMemoryBus _publisher;
            protected PartitionStateCache _stateCache;
            protected string _resultStreamName;
            protected FakeCoreProjection _coreProjection;

            [SetUp]
            public void SetUp()
            {
                _stateCache = GivenStateCache();
                _publisher = new InMemoryBus("test");
                _coreProjection = new FakeCoreProjection();
                _checkpointManager = new specification_with_multi_phase_core_projection.FakeCheckpointManager(
                    _publisher, Guid.NewGuid());
                _resultStreamName = "result-stream";
                _phase = new WriteQueryResultProjectionProcessingPhase(
                    1, _resultStreamName, _coreProjection, _stateCache, _checkpointManager, _checkpointManager);
                When();
            }

            protected virtual PartitionStateCache GivenStateCache()
            {
                var stateCache = new PartitionStateCache();

                stateCache.CachePartitionState(
                    "a", new PartitionState("{}", null, CheckpointTag.FromPhase(0, completed: false)));
                stateCache.CachePartitionState(
                    "b", new PartitionState("{}", null, CheckpointTag.FromPhase(0, completed: false)));
                stateCache.CachePartitionState(
                    "c", new PartitionState("{}", null, CheckpointTag.FromPhase(0, completed: false)));
                return stateCache;
            }

            protected abstract void When();

            [TearDown]
            public void TearDown()
            {
                _phase = null;
            }
        }

        [TestFixture]
        class when_created : specification_with_write_query_result_projection_processing_phase
        {
            protected override void When()
            {
            }

            [Test]
            public void can_be_initialized_from_phase_checkpoint()
            {
                _phase.InitializeFromCheckpoint(CheckpointTag.FromPhase(1, completed: false));
            }

            [Test, ExpectedException(typeof (InvalidOperationException))]
            public void process_event_throws_invalid_operation_exception()
            {
                _phase.ProcessEvent();
            }
        }

        [TestFixture]
        class when_subscribing : specification_with_write_query_result_projection_processing_phase
        {
            protected override void When()
            {
                _phase.Subscribe(CheckpointTag.FromPhase(1, completed: false), false);
            }

            [Test]
            public void notifies_core_projection_with_subscribed()
            {
                Assert.AreEqual(1, _coreProjection.SubscribedInvoked);
            }
        }

        [TestFixture]
        class when_processing_event : specification_with_write_query_result_projection_processing_phase
        {
            protected override void When()
            {
                _phase.Subscribe(CheckpointTag.FromPhase(1, completed: false), false);
                _phase.SetProjectionState(PhaseState.Running);
                _phase.ProcessEvent();
            }

            [Test]
            public void writes_query_results()
            {
                Assert.AreEqual(3, _checkpointManager.EmittedEvents.Count(v => v.Event.EventType == "Result"));
            }
        }

        [TestFixture]
        class when_completed_query_processing_event : specification_with_write_query_result_projection_processing_phase
        {
            protected override void When()
            {
                _phase.Subscribe(CheckpointTag.FromPhase(1, completed: false), false);
                _phase.SetProjectionState(PhaseState.Running);
                _phase.ProcessEvent();
                _phase.SetProjectionState(PhaseState.Stopped);
                _phase.ProcessEvent();
            }

            [Test]
            public void writes_query_results_only_once()
            {
                Assert.AreEqual(3, _checkpointManager.EmittedEvents.Count(v => v.Event.EventType == "Result"));
            }
        }
    }
}
