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
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.checkpoint_manager.multi_stream
{
    [TestFixture]
    public class when_starting_with_prerecorded_events_after_the_last_checkpoint: TestFixtureWithMultiStreamCheckpointManager
    {
        protected override void Given()
        {
            base.Given();
            ExistingEvent("$projections-projection-checkpoint", "ProjectionCheckpoint", @"{""Streams"": {""a"": 0, ""b"": 0, ""c"": 0}}", "{}");
            ExistingEvent("a", "StreamCreated", "", "");
            ExistingEvent("b", "StreamCreated", "", "");
            ExistingEvent("c", "StreamCreated", "", "");

            ExistingEvent("a", "Event", "", @"{""data"":""a""");
            ExistingEvent("b", "Event", "", @"{""data"":""b""");
            ExistingEvent("c", "Event", "", @"{""data"":""c""");

            ExistingEvent("$projections-projection-order", "$>", @"{""Streams"": {""a"": 0, ""b"": 0, ""c"": 1}}", "1@c");
            ExistingEvent("$projections-projection-order", "$>", @"{""Streams"": {""a"": 1, ""b"": 0, ""c"": 1}}", "1@a");
            ExistingEvent("$projections-projection-order", "$>", @"{""Streams"": {""a"": 1, ""b"": 1, ""c"": 1}}", "1@b");
        }

        protected override void When()
        {
            base.When();
            _manager.BeginLoadState();
        }

        [Test]
        public void publishes_correct_checkpoint_loaded_message()
        {
        }
    }
}
