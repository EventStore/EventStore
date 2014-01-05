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

namespace EventStore.Projections.Core.Tests.Services.integration.scenarios
{
    [TestFixture]
    public class when_recategorizing_chat_events_by_users : specification_with_a_v8_query_posted
    {
        protected override void GivenEvents()
        {
        }

        protected override IEnumerable<WhenStep> When()
        {
            foreach (var e in base.When()) yield return e;
            yield return CreateWriteEvent("chat-1", "ChatMessage", @"
    {
      ""sender"": ""Greg"",
      ""message"": ""Hi"",
      ""time"": ""03:45:30""
    }");
            yield return CreateWriteEvent("chat-1", "ChatMessage", @"
    {
      ""sender"": ""Ronan"",
      ""message"": ""starbucks"",
      ""time"": ""03:45:31""
    }");
            yield return CreateWriteEvent("chat-1", "ChatMessage", @"
    {
      ""sender"": ""James"",
      ""message"": ""herpherp"",
      ""time"": ""03:45:32""
    }");
            yield return CreateWriteEvent("chat-2", "ChatMessage", @"
    {
      ""sender"": ""Rob"",
      ""message"": ""starbucks"",
      ""time"": ""03:45:30""
    }");
            yield return CreateWriteEvent("chat-2", "ChatMessage", @"
    {
      ""sender"": ""Ronan"",
      ""message"": ""put the moose in the chocolate"",
      ""time"": ""03:45:31""
    }");
            yield return CreateQueryMessage("query1", @"
fromCategory(""chat"")
    .foreachStream()
    .when({
        $init: function() {
            return { count: 0 }
        },
        ChatMessage: function(state, event) {
            state.count += 1;
            return state;
        }
    });
");
            yield return CreateQueryMessage("query2", @"
fromCategory(""user"")
    .foreachStream()
    .when({
        $init: function() {
            return { count: 0 }
        },
        ChatMessage: function(state, event) {
            state.count += 1;
            return state;
        }
    });
");
            yield return CreateQueryMessage("query3", @"
options({disableParallelism: true});
fromCategory(""user"")
    .foreachStream()
    .when({
        $init: function() {
            return { count: 0 }
        },
        ChatMessage: function(state, event) {
            state.count += 1;
            return state;
        }
    });
");
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
            return "";
        }

        protected override IEnumerable<string> GivenOtherProjections()
        {
            yield return @"
fromCategory(""chat"")
.when({
    ChatMessage: function(state, event) {
        var userStream = ""user-"" + event.body.sender;
        linkTo(userStream, event);
    }
})";
        }

        [Test]
        public void query1_returns_correct_result()
        {
            AssertStreamTailWithLinks(
                "$projections-query1-result", @"Result:{""count"":3}", @"Result:{""count"":2}", "$Eof:");
        }

        [Test]
        public void query2_returns_correct_result()
        {
            AssertStreamTailWithLinks(
                "$projections-query2-result", @"Result:{""count"":1}", @"Result:{""count"":2}", @"Result:{""count"":1}",
                @"Result:{""count"":1}", "$Eof:");
        }

        [Test]
        public void query3_returns_correct_result()
        {
            AssertStreamTailWithLinks(
                "$projections-query3-result", @"Result:{""count"":1}", @"Result:{""count"":2}", @"Result:{""count"":1}",
                @"Result:{""count"":1}", "$Eof:");
        }

    }
}
