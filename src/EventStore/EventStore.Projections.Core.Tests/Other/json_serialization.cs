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
using System.Collections.Generic;
using System.Text;
using EventStore.Common.Utils;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using System.Linq;
using Newtonsoft.Json.Linq;
using EventStore.Core.Util;

namespace EventStore.Projections.Core.Tests.Other
{
    [TestFixture]
    class can_serialize_and_deserialize
    {
        private ProjectionVersion _version;

        [SetUp]
        public void setup()
        {
            _version = new ProjectionVersion(1, 0, 0);
        }

        [Test]
        public void position_based_checkpoint_tag()
        {
            CheckpointTag tag = CheckpointTag.FromPosition(-1, 0);
            byte[] bytes = tag.ToJsonBytes(_version);
            string instring = Encoding.UTF8.GetString(bytes);
            Console.WriteLine(instring);

            CheckpointTag back = instring.ParseCheckpointTagJson();
            Assert.AreEqual(tag, back);
        }

        [Test]
        public void prepare_position_based_checkpoint_tag_zero()
        {
            CheckpointTag tag = CheckpointTag.FromPreparePosition(0);
            byte[] bytes = tag.ToJsonBytes(_version);
            string instring = Encoding.UTF8.GetString(bytes);
            Console.WriteLine(instring);

            CheckpointTag back = instring.ParseCheckpointTagJson();
            Assert.AreEqual(tag, back);
        }

        [Test]
        public void prepare_position_based_checkpoint_tag_minus_one()
        {
            CheckpointTag tag = CheckpointTag.FromPreparePosition(-1);
            byte[] bytes = tag.ToJsonBytes(_version);
            string instring = Encoding.UTF8.GetString(bytes);
            Console.WriteLine(instring);

            CheckpointTag back = instring.ParseCheckpointTagJson();
            Assert.AreEqual(tag, back);
        }

        [Test]
        public void prepare_position_based_checkpoint_tag()
        {
            CheckpointTag tag = CheckpointTag.FromPreparePosition(1234);
            byte[] bytes = tag.ToJsonBytes(_version);
            string instring = Encoding.UTF8.GetString(bytes);
            Console.WriteLine(instring);

            CheckpointTag back = instring.ParseCheckpointTagJson();
            Assert.AreEqual(tag, back);
        }

        [Test]
        public void stream_based_checkpoint_tag()
        {
            CheckpointTag tag = CheckpointTag.FromStreamPosition("$ce-account", 12345);
            byte[] bytes = tag.ToJsonBytes(_version);
            string instring = Encoding.UTF8.GetString(bytes);
            Console.WriteLine(instring);

            CheckpointTag back = instring.ParseCheckpointTagJson();
            Assert.AreEqual(tag, back);
            Assert.IsNull(back.CommitPosition);
        }

        [Test]
        public void streams_based_checkpoint_tag()
        {
            CheckpointTag tag = CheckpointTag.FromStreamPositions(new Dictionary<string, int>{{"a", 1}, {"b", 2}});
            byte[] bytes = tag.ToJsonBytes(_version);
            string instring = Encoding.UTF8.GetString(bytes);
            Console.WriteLine(instring);

            CheckpointTag back = instring.ParseCheckpointTagJson();
            Assert.AreEqual(tag, back);
            Assert.IsNull(back.CommitPosition);
        }

        [Test]
        public void extra_metadata_are_preserved()
        {
            CheckpointTag tag = CheckpointTag.FromPosition(-1, 0);
            var extra = new Dictionary<string, string> {{"$$a", "\"b\""}, {"$$c", "\"d\""}};
            byte[] bytes = tag.ToJsonBytes(_version, extra);
            string instring = Encoding.UTF8.GetString(bytes);
            Console.WriteLine(instring);

            CheckpointTagVersion back = instring.ParseCheckpointTagJson(_version);
            Assert.IsNotNull(back.ExtraMetadata);
            JToken v;
            Assert.IsTrue(back.ExtraMetadata.TryGetValue("$$a", out v));
            Assert.AreEqual("b", (string) ((JValue) v).Value);
            Assert.IsTrue(back.ExtraMetadata.TryGetValue("$$c", out v));
            Assert.AreEqual("d", (string)((JValue)v).Value);
        }

        [Test]
        public void can_deserialize_readonly_fields()
        {
            TestData data = new TestData("123");
            byte[] bytes = data.ToJsonBytes();
            string instring = Encoding.UTF8.GetString(bytes);
            Console.WriteLine(instring);

            TestData back = instring.ParseJson<TestData>();
            Assert.AreEqual(data, back);
        }

        private class TestData
        {
            public readonly string Data;

            public TestData(string data)
            {
                Data = data;
            }

            protected bool Equals(TestData other)
            {
                return string.Equals(Data, other.Data);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((TestData) obj);
            }

            public override int GetHashCode()
            {
                return (Data != null ? Data.GetHashCode() : 0);
            }

            public override string ToString()
            {
                return string.Format("Data: {0}", Data);
            }
        }
    }
}
