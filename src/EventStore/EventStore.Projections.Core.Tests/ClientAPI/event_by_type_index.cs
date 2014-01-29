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

using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.ClientAPI
{
    public class event_by_type_index
    {

        public class with_existing_events : specification_with_standard_projections_runnning
        {
            protected override void Given()
            {
                base.Given();
                PostEvent("stream1", "type1", "{}");
                PostEvent("stream1", "type2", "{}");
                PostEvent("stream1", "type3", "{}");
                PostEvent("stream2", "type1", "{}");
                PostEvent("stream2", "type2", "{}");
                PostEvent("stream2", "type3", "{}");
            }


        }

        [TestFixture, Category("LongRunning")]
        public class when_creating : with_existing_events
        {
            protected override void When()
            {
                base.When();
                var query = @"
fromAll().when({
    $init: function(){
        return {c: 0};
    },
    type1: count,
    type2: count
}).outputState()

function count(s,e) {
    return {c: s.c + 1};
}
";
                _manager.CreateContinuous("test-projection", query, _admin);
                WaitIdle();

            }

            [Test, Category("Network")]
            public void result_is_correct()
            {
                AssertStreamTail("$projections-test-projection-result", "Result:{\"c\":4}");
            }

        }

    }
}
