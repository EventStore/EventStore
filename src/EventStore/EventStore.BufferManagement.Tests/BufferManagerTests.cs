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
using NUnit.Framework;

namespace EventStore.BufferManagement.Tests
{
    [TestFixture]
    public class when_creating_a_buffer_manager
    {
        [Test]
        public void a_zero_chunk_size_causes_an_argumentexception()
        {
            Assert.Throws<ArgumentException>(() => new BufferManager(1024, 0, 1024));
        }

        [Test]
        public void a_negative_chunk_size_causes_an_argumentexception()
        {
            Assert.Throws<ArgumentException>(() => new BufferManager(200, -1, 200));
        }

        [Test]
        public void a_negative_chunks_per_segment_causes_an_argumentexception()
        {
            Assert.Throws<ArgumentException>(() => new BufferManager(-1, 1024, 8));
        }

        [Test]
        public void a_zero_chunks_per_segment_causes_an_argumentexception()
        {
            Assert.Throws<ArgumentException>(() => new BufferManager(0, 1024, 8));
        }

        [Test]
        public void a_negative_number_of_segments_causes_an_argumentexception()
        {
            Assert.Throws<ArgumentException>(() => new BufferManager(1024, 1024, -1));
        }

        [Test]
        public void can_create_a_manager_with_zero_inital_segments()
        {
            Assert.DoesNotThrow(() => new BufferManager(1024, 1024, 0));
        }
    }

    [TestFixture]
    public class when_checking_out_a_buffer
    {
        [Test]
        public void should_return_a_valid_buffer_when_available()
        {
            BufferManager manager = new BufferManager(1, 1000, 1);
            ArraySegment<byte> buffer = manager.CheckOut();
            Assert.AreEqual(1000, buffer.Count);
        }

        [Test]
        public void should_decrement_available_buffers()
        {
            BufferManager manager = new BufferManager(1, 1000, 1);
            manager.CheckOut();
            Assert.AreEqual(0, manager.AvailableBuffers);
        }

        [Test]
        public void should_create_a_segment_if_none_are_availabke()
        {
            BufferManager manager = new BufferManager(10, 1000, 0);
            manager.CheckOut();
            Assert.AreEqual(9, manager.AvailableBuffers);
        }

        [Test]
        public void should_throw_an_unabletocreatememoryexception_if_acquiring_memory_is_disabled_and_out_of_memory()
        {
            BufferManager manager = new BufferManager(1, 1000, 1, false);
            manager.CheckOut();
            //should be none left, boom
            Assert.Throws<UnableToCreateMemoryException>(() => manager.CheckOut());
        }
    }

    [TestFixture]
    public class when_checking_in_a_buffer
    {
        [Test]
        public void should_accept_a_checked_out_buffer()
        {
            BufferManager manager = new BufferManager(10, 1000, 0);
            manager.CheckIn(manager.CheckOut());
        }

        [Test]
        public void should_increment_available_buffers()
        {
            BufferManager manager = new BufferManager(10, 1000, 0);
            manager.CheckIn(manager.CheckOut());
            Assert.AreEqual(10, manager.AvailableBuffers);
        }

        [Test, ExpectedException(typeof(ArgumentNullException))]
        public void should_throw_argumentnullexception_if_null_buffer()
        {
            BufferManager manager = new BufferManager(10, 1000, 0);
            manager.CheckIn(null);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void should_throw_argumentexception_if_buffer_wrong_size()
        {
            BufferManager manager = new BufferManager(10, 1000, 0);
            byte[] data = new byte[10000];
            manager.CheckIn(new ArraySegment<byte>(data));
        }
    }
}
