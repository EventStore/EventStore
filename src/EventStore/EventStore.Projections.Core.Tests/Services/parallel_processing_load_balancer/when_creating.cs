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
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.parallel_processing_load_balancer
{
    [TestFixture]
    public class when_creating
    {
        [Test]
        public void can_be_created()
        {
            var b = new ParallelProcessingLoadBalancer(2, 10, 1);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void zero_workers_throws_argument_exception()
        {
            var b = new ParallelProcessingLoadBalancer(0, 10, 1);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void negative_workers_throws_argument_exception()
        {
            var b = new ParallelProcessingLoadBalancer(-1, 10, 1);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void zero_max_scheduled_size_throws_argument_exception()
        {
            var b = new ParallelProcessingLoadBalancer(2, 0, 1);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void negative_max_scheduled_size_throws_argument_exception()
        {
            var b = new ParallelProcessingLoadBalancer(2, -1, 1);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void zero_max_unmeasured_tasks_throws_argument_exception()
        {
            var b = new ParallelProcessingLoadBalancer(2, 10, 0);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void negative_max_unmeasured_tasks_throws_argument_exception()
        {
            var b = new ParallelProcessingLoadBalancer(2, 10, -2);
        }


    }
}
