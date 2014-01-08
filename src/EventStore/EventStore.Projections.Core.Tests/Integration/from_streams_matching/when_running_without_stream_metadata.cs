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
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Integration.from_streams_matching
{
    [TestFixture]
    public class when_running_without_stream_metadata : specification_with_a_v8_query_posted
    {
        protected override void GivenEvents()
        {
            ExistingEvent("stream1", "event", "{}", "{\"data\":1}", isJson: true);
            ExistingEvent("stream2", "event", "{}", "{\"data\":2}", isJson: true);
            ExistingEvent("stream2", "event", "{}", "{\"data\":21}", isJson: true);
            ExistingEvent("stream3", "event", "{}", "{\"data\":3}", isJson: true);
            ExistingEvent("other1", "other-event", "{}", "{\"data\":1}", isJson: true);
            ExistingEvent("other2", "other-event", "{}", "{\"data\":2}", isJson: true);
        }

        protected override IEnumerable<WhenStep> When()
        {
            foreach (var e in base.When()) yield return e;
        }

        protected override bool GivenInitializeSystemProjections()
        {
            return true;
        }

        protected override bool GivenStartSystemProjections()
        {
            return true;
        }

        protected override string GivenQuery()
        {
            return @"
fromStreamsMatching(function(s, streamMeta){
        return s.indexOf(""stream"") === 0;
}).when({
    $any: function(s, e) {
        return {data: e.data.data};
    }
})";
        }

        [Test]
        public void query_returns_correct_result()
        {
            AssertStreamTailWithLinks(
                "$projections-query-result", @"Result:{""data"":1}", @"Result:{""data"":21}", @"Result:{""data"":3}",
                "$Eof:");
        }

    }
}
