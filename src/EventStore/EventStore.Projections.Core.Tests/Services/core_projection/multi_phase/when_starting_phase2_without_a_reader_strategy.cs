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

using System.Linq;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.multi_phase
{
    [TestFixture]
    class when_starting_phase2_without_a_reader_strategy : specification_with_multi_phase_core_projection
    {
        protected override FakeReaderStrategy GivenPhase2ReaderStrategy()
        {
            return null;
        }

        protected override void When()
        {
            _coreProjection.Start();
            _coreProjection.Handle(
                new EventReaderSubscriptionMessage.EofReached(
                    Phase1.SubscriptionId, CheckpointTag.FromPosition(0, 500, 450), 0));
        }

        [Test]
        public void initializes_phase2()
        {
            Assert.IsTrue(Phase2.Initialized);
        }

        [Test]
        public void updates_checkpoint_tag_phase()
        {
            Assert.AreEqual(1, _coreProjection.LastProcessedEventPosition.Phase);
        }

        [Test]
        public void starts_processing_phase2()
        {
            Assert.AreEqual(1, Phase2.ProcessEventInvoked);
        }
    }
}
