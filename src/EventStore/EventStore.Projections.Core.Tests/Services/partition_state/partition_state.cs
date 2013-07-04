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
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.partition_state
{
    public static class partition_state
    {
        [TestFixture]
        public class when_creating
        {
            [Test, ExpectedException(typeof(ArgumentNullException))]
            public void throws_argument_null_exception_if_state_is_null()
            {
                new PartitionState(null, "result", CheckpointTag.FromPosition(100, 50));
            }

            [Test, ExpectedException(typeof(ArgumentNullException))]
            public void throws_argument_null_exception_if_caused_by_is_null()
            {
                new PartitionState("state", "result", null);
            }

            [Test]
            public void can_be_created()
            {
                new PartitionState("state", "result", CheckpointTag.FromPosition(100, 50));
            }
        }

        [TestFixture]
        public class can_be_deserialized_from_serialized_form
        {
            [Test]
            public void simple_object()
            {
                AssertCorrect(@"{""a"":""b""}");
                AssertCorrect(@"{""a"":""b"",""c"":1}");
                AssertCorrect(@"{""z"":null,""a"":""b"",""c"":1}");
            }

            [Test]
            public void complex_object()
            {
                AssertCorrect(@"{""a"":""b"",""c"":[1,2,3]}");
                AssertCorrect(@"{""a"":""b"",""c"":{""a"":""b""}}");
                AssertCorrect(@"{""a"":""b"",""c"":[{},[],null]}");
            }

            [Test]
            public void null_deserialization()
            {
                var deserialized = PartitionState.Deserialize(null, CheckpointTag.FromPosition(100, 50));
                Assert.AreEqual("", deserialized.State);
                Assert.IsNull(deserialized.Result);
            }

            private void AssertCorrect(string state, string result = null)
            {
                var partitionState = new PartitionState(state, result, CheckpointTag.FromPosition(100, 50));
                var serialized = partitionState.Serialize();
                var deserialized = PartitionState.Deserialize(serialized, CheckpointTag.FromPosition(100, 50));

                Assert.AreEqual(partitionState.State, deserialized.State);
                Assert.AreEqual(partitionState.Result, deserialized.Result);
            }
        }
    }
}
