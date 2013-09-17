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
using System.Linq;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.parallel_processing_load_balancer
{
    [TestFixture]
    public class when_scheduling_first_tasks : specification_with_parallel_processing_load_balancer
    {
        private List<string> _scheduledTasks;
        private List<int> _scheduledOnWorkers;
        private int _scheduled;

        protected override void Given()
        {
            _scheduled = 0;
            _scheduledTasks = new List<string>();
            _scheduledOnWorkers = new List<int>();
        }

        protected override void When()
        {
            _balancer.ScheduleTask("task1", OnScheduled);
            _balancer.ScheduleTask("task2", OnScheduled);
        }

        private void OnScheduled(string task, int worker)
        {
            _scheduled++;
            _scheduledTasks.Add(task);
            _scheduledOnWorkers.Add(worker);
        }

        [Test]
        public void schedules_all_tasks()
        {
            Assert.AreEqual(2, _scheduled);
        }

        [Test]
        public void schedules_correct_tasks()
        {
            Assert.That(new[] {"task1", "task2"}.SequenceEqual(_scheduledTasks));
        }

        [Test]
        public void schedules_on_different_workers()
        {
            Assert.That(_scheduledOnWorkers.Distinct().Count() == 2);
        }
    }
}
