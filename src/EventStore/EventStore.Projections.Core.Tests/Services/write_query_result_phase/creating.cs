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

using EventStore.Projections.Core.Services.Processing;
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
                var it = new WriteQueryResultProjectionProcessingPhase();
            }
        }

        abstract class specification_with_write_query_result_projection_processing_phase
        {
            private WriteQueryResultProjectionProcessingPhase _phase;

            public WriteQueryResultProjectionProcessingPhase Phase
            {
                get { return _phase; }
            }

            [SetUp]
            public void SetUp()
            {
                _phase = new WriteQueryResultProjectionProcessingPhase();
                When();
            }

            protected abstract void When();

            [TearDown]
            public void TearDown()
            {
                _phase = null;
            }
        }

        [TestFixture]
        class when_created: specification_with_write_query_result_projection_processing_phase
        {
            protected override void When()
            {
            }

            [Test]
            public void can_be_initialized_from_phase_checkpoint()
            {
                Phase.InitializeFromCheckpoint(CheckpointTag.FromPhase(1));
            }

            [Test]
            public void can_process_event()
            {
                Phase.ProcessEvent();
            }
        }

        [TestFixture]
        class when_process_event : specification_with_write_query_result_projection_processing_phase
        {
            protected override void When()
            {
                Phase.ProcessEvent();
            }

            [Test]
            public void writes_query_results()
            {
                Assert.Inconclusive();
            }
        }
    }
}
