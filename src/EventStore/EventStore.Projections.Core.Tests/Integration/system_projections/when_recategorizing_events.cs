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
using EventStore.Core.Services;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Integration.system_projections
{
    [TestFixture]
    public class when_recategorizing_events : specification_with_a_v8_query_posted
    {
        protected override void GivenEvents()
        {
            ExistingEvent("account-01", "test", "", "{\"a\":1}", isJson: true);
            ExistingEvent("account-01", "test", "", "{\"a\":2}", isJson: true);
            ExistingEvent("account-02", "test", "", "{\"a\":10}", isJson: true);

            ExistingEvent("categorized-0", SystemEventTypes.LinkTo, "{\"a\":2}", "1@account-01");
            ExistingEvent("categorized-0", SystemEventTypes.LinkTo, "{\"a\":10}", "0@account-02");
            ExistingEvent("categorized-1", SystemEventTypes.LinkTo, "{\"a\":1}", "0@account-01");

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
            return "";
        }

        [Test]
        public void streams_are_categorized()
        {
            AssertStreamTail("$category-account", "account-01", "account-02");
            AssertStreamTail("$category-categorized", "categorized-0", "categorized-1");
        }

        [Test]
        public void events_are_categorized()
        {
            AssertStreamTail("$ce-account", "0@account-01", "1@account-01", "0@account-02");
        }

        [Test]
        public void links_are_categorized()
        {
            AssertStreamTail("$ce-categorized", "1@account-01", "0@account-02", "0@account-01");
        }

    }
}
