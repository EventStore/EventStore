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

using EventStore.Core.Services.Transport.Http;
using EventStore.Transport.Http;
using NUnit.Framework;

namespace EventStore.Core.Tests.Http
{
    [TestFixture]
    public class accept_component
    {
        [Test]
        public void parses_generic_wildcard()
        {
            var c = new AcceptComponent("*/*");

            Assert.AreEqual("*", c.MediaType);
            Assert.AreEqual("*", c.MediaSubtype);
            Assert.AreEqual("*/*", c.MediaRange);
            Assert.AreEqual(1.0f, c.Priority);
        }

        [Test]
        public void parses_generic_wildcard_with_priority()
        {
            var c = new AcceptComponent("*/*;q=0.4");

            Assert.AreEqual("*", c.MediaType);
            Assert.AreEqual("*", c.MediaSubtype);
            Assert.AreEqual("*/*", c.MediaRange);
            Assert.AreEqual(0.4f, c.Priority);
        }

        [Test]
        public void parses_generic_wildcard_with_priority_and_other_param()
        {
            var c = new AcceptComponent("*/*;q=0.4;z=7");

            Assert.AreEqual("*", c.MediaType);
            Assert.AreEqual("*", c.MediaSubtype);
            Assert.AreEqual("*/*", c.MediaRange);
            Assert.AreEqual(0.4f, c.Priority);
        }

        [Test]
        public void parses_partial_wildcard()
        {
            var c = new AcceptComponent("text/*");

            Assert.AreEqual("text", c.MediaType);
            Assert.AreEqual("*", c.MediaSubtype);
            Assert.AreEqual("text/*", c.MediaRange);
            Assert.AreEqual(1f, c.Priority);
        }

        [Test]
        public void parses_specific_media_range_with_priority()
        {
            var c = new AcceptComponent("application/xml;q=0.7");

            Assert.AreEqual("application", c.MediaType);
            Assert.AreEqual("xml", c.MediaSubtype);
            Assert.AreEqual("application/xml", c.MediaRange);
            Assert.AreEqual(0.7f, c.Priority);
        }

    }
}
