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
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.integration.parallel_query
{
    [TestFixture]
    public class when_running_from_catalog_stream_query_with_transform : specification_with_a_v8_query_posted
    {
        protected override void GivenEvents()
        {
            ExistingEvent("catalog", "A", "", "{\"a\":\"01\"}", isJson: true);
            ExistingEvent("catalog", "A", "", "{\"a\":\"02\"}", isJson: true);
            ExistingEvent("catalog", "A", "", "{\"a\":\"03\"}", isJson: true);

            ExistingEvent("account-01", "test", "", "{}");
            ExistingEvent("account-01", "test", "", "{}");
            ExistingEvent("account-03", "test", "", "{}");
            ExistingEvent("account-03", "test", "", "{}");
            ExistingEvent("account-03", "test", "", "{}");
        }

        protected override string GivenQuery()
        {
            return @"
fromStreamCatalog('catalog', function(ev) {log(JSON.stringify(ev)); return 'account-' + ev.body.a;}).foreachStream().when({
    $init: function() { return {c: 0}; },
    $any: function(s, e) { return {c: s.c + 1}; }
})
";
        }

        [Test]
        public void just()
        {
            AssertLastEvent("$projections-query-account-01-result", "{\"c\":2}");
//            AssertLastEvent("$projections-query-account-02-result", "{\"c\":0}");
            AssertLastEvent("$projections-query-account-03-result", "{\"c\":3}");
        }

        [Test]
        public void state_becomes_completed()
        {
            _manager.Handle(
                new ProjectionManagementMessage.GetStatistics(new PublishEnvelope(_bus), null, _projectionName, false));

            Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Count());
            Assert.AreEqual(
                1,
                _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Single().Projections.Length);
            Assert.AreEqual(
                _projectionName,
                _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
                    .Single()
                    .Projections.Single()
                    .Name);
            Assert.AreEqual(
                ManagedProjectionState.Completed,
                _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
                    .Single()
                    .Projections.Single()
                    .MasterStatus);
        }
    }
}
