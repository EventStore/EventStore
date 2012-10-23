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
using System.IO;
using NUnit.Framework;

namespace EventStore.BufferManagement.Tests
{
    public class has_buffer_pool_fixture : has_buffer_manager_fixture
    {
        protected BufferPool BufferPool;
        [SetUp]
        public override void Setup()
        {
            base.Setup();
            BufferPool = new BufferPool(10, BufferManager);
        }
    }

    [TestFixture]
    public class when_insantiating_a_buffer_pool_stream : has_buffer_pool_fixture
    {
        [Test, ExpectedException(typeof(ArgumentNullException))]
        public void a_null_buffer_pool_throws_an_argumentnullexception()
        {
            BufferPoolStream stream = new BufferPoolStream(null);
        }

        [Test]
        public void the_internal_buffer_pool_is_set()
        {
            BufferPoolStream stream = new BufferPoolStream(BufferPool);
            Assert.AreEqual(BufferPool, stream.BufferPool);
        }

    }

    [TestFixture]
    public class when_reading_from_the_stream : has_buffer_pool_fixture
    {
        [Test]
        public void position_is_incremented()
        {
            BufferPoolStream stream = new BufferPoolStream(BufferPool);
            stream.Write(new byte[500], 0, 500);
            stream.Seek(0, SeekOrigin.Begin);
            Assert.AreEqual(0, stream.Position);
            int read = stream.Read(new byte[50], 0, 50);
            Assert.AreEqual(50, stream.Position);
        }

        [Test]
        public void a_read_past_the_end_of_the_stream_returns_zero()
        {
            BufferPoolStream stream = new BufferPoolStream(BufferPool);
            stream.Write(new byte[500], 0, 500);
            stream.Position = 0;
            int read = stream.Read(new byte[500], 0, 500);
            Assert.AreEqual(500, read);
            read = stream.Read(new byte[500], 0, 500);
            Assert.AreEqual(0, read);
        }
    }

    [TestFixture]
    public class when_writing_to_the_stream : has_buffer_pool_fixture
    {
        [Test]
        public void position_is_incremented()
        {
            BufferPoolStream stream = new BufferPoolStream(BufferPool);
            stream.Write(new byte[500], 0, 500);
            Assert.AreEqual(500, stream.Position);
        }
    }

    [TestFixture]
    public class when_seeking_in_the_stream : has_buffer_pool_fixture
    {
        [Test]
        public void from_begin_sets_relative_to_beginning()
        {
            BufferPoolStream stream = new BufferPoolStream(BufferPool);
            stream.Write(new byte[500], 0, 500);
            stream.Seek(22, SeekOrigin.Begin);
            Assert.AreEqual(22, stream.Position);
        }

        [Test]
        public void from_end_sets_relative_to_end()
        {
            BufferPoolStream stream = new BufferPoolStream(BufferPool);
            stream.Write(new byte[500], 0, 500);
            stream.Seek(-100, SeekOrigin.End);
            Assert.AreEqual(400, stream.Position);
        }
        
        [Test]
        public void from_current_sets_relative_to_current()
        {
            BufferPoolStream stream = new BufferPoolStream(BufferPool);
            stream.Write(new byte[500], 0, 500);
            stream.Seek(-2, SeekOrigin.Current);
            stream.Seek(1, SeekOrigin.Current);
            Assert.AreEqual(499, stream.Position);
        }

        [Test, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void a_negative_position_throws_an_argumentexception()
        {
            BufferPoolStream stream = new BufferPoolStream(BufferPool);
            stream.Seek(-1, SeekOrigin.Begin);
        }

        [Test, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void seeking_past_end_of_stream_throws_an_argumentexception()
        {
            BufferPoolStream stream = new BufferPoolStream(BufferPool);
            stream.Write(new byte[500], 0, 500);
            stream.Seek(501, SeekOrigin.Begin);
        }
    }

}
