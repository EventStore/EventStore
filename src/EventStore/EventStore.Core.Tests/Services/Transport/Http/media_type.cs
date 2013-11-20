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

using EventStore.Common.Utils;
using EventStore.Transport.Http;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Transport.Http
{
    [TestFixture]
    public class media_type
    {
        [Test]
        public void parses_generic_wildcard()
        {
            var c = MediaType.Parse("*/*");

            Assert.AreEqual("*", c.Type);
            Assert.AreEqual("*", c.Subtype);
            Assert.AreEqual("*/*", c.Range);
            Assert.AreEqual(1.0f, c.Priority);
            Assert.IsFalse(c.EncodingSpecified);
            Assert.IsNull(c.Encoding);
        }

        [Test]
        public void parses_generic_wildcard_with_priority()
        {
            var c = MediaType.Parse("*/*;q=0.4");

            Assert.AreEqual("*", c.Type);
            Assert.AreEqual("*", c.Subtype);
            Assert.AreEqual("*/*", c.Range);
            Assert.AreEqual(0.4f, c.Priority);
            Assert.IsFalse(c.EncodingSpecified);
            Assert.IsNull(c.Encoding);
        }

        [Test]
        public void parses_generic_wildcard_with_priority_and_other_param()
        {
            var c = MediaType.Parse("*/*;q=0.4;z=7");

            Assert.AreEqual("*", c.Type);
            Assert.AreEqual("*", c.Subtype);
            Assert.AreEqual("*/*", c.Range);
            Assert.AreEqual(0.4f, c.Priority);
            Assert.IsFalse(c.EncodingSpecified);
            Assert.IsNull(c.Encoding);
        }

        [Test]
        public void parses_partial_wildcard()
        {
            var c = MediaType.Parse("text/*");

            Assert.AreEqual("text", c.Type);
            Assert.AreEqual("*", c.Subtype);
            Assert.AreEqual("text/*", c.Range);
            Assert.AreEqual(1f, c.Priority);
            Assert.IsFalse(c.EncodingSpecified);
            Assert.IsNull(c.Encoding);
        }

        [Test]
        public void parses_specific_media_range_with_priority()
        {
            var c = MediaType.Parse("application/xml;q=0.7");

            Assert.AreEqual("application", c.Type);
            Assert.AreEqual("xml", c.Subtype);
            Assert.AreEqual("application/xml", c.Range);
            Assert.AreEqual(0.7f, c.Priority);
            Assert.IsFalse(c.EncodingSpecified);
            Assert.IsNull(c.Encoding);
        }

        [Test]
        public void parses_with_encoding()
        {
            var c = MediaType.Parse("application/json;charset=utf-8");

            Assert.AreEqual("application", c.Type);
            Assert.AreEqual("json", c.Subtype);
            Assert.AreEqual("application/json", c.Range);
            Assert.IsTrue(c.EncodingSpecified);
            Assert.AreEqual(Helper.UTF8NoBom, c.Encoding);
        }

        [Test]
        public void parses_with_encoding_and_priority()
        {
            var c = MediaType.Parse("application/json;q=0.3;charset=utf-8");

            Assert.AreEqual("application", c.Type);
            Assert.AreEqual("json", c.Subtype);
            Assert.AreEqual("application/json", c.Range);
            Assert.AreEqual(0.3f, c.Priority);
            Assert.IsTrue(c.EncodingSpecified);
            Assert.AreEqual(Helper.UTF8NoBom, c.Encoding);
        }

        [Test]
        public void parses_unknown_encoding()
        {
            var c = MediaType.Parse("application/json;charset=woftam");

            Assert.AreEqual("application", c.Type);
            Assert.AreEqual("json", c.Subtype);
            Assert.AreEqual("application/json", c.Range);
            Assert.IsTrue(c.EncodingSpecified);
            Assert.IsNull(c.Encoding);
        }

        [Test]
        public void parses_with_spaces_between_parameters()
        {
            var c = MediaType.Parse("application/json; charset=woftam");

            Assert.AreEqual("application", c.Type);
            Assert.AreEqual("json", c.Subtype);
            Assert.AreEqual("application/json", c.Range);
            Assert.IsTrue(c.EncodingSpecified);
            Assert.IsNull(c.Encoding);
        }

        [Test]
        public void parses_upper_case_parameters()
        {
            var c = MediaType.Parse("application/json; charset=UTF-8");

            Assert.AreEqual("application", c.Type);
            Assert.AreEqual("json", c.Subtype);
            Assert.AreEqual("application/json", c.Range);
            Assert.IsTrue(c.EncodingSpecified);
            Assert.AreEqual(Helper.UTF8NoBom, c.Encoding);
        }
    }
}
