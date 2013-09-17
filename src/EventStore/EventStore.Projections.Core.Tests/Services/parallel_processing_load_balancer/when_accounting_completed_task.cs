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

using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.parallel_processing_load_balancer
{
    [TestFixture]
    public class when_accounting_completed_task : specification_with_parallel_processing_load_balancer
    {
        private int _task1ScheduledOn;
        private int _task2ScheduledOn;

        protected override void Given()
        {
            _balancer.ScheduleTask("task1", OnScheduled);
            _balancer.ScheduleTask("task2", OnScheduled);
            _balancer.AccountMeasured("task1", 1000);
            _balancer.AccountMeasured("task2", 100);
        }

        protected override void When()
        {
            _balancer.AccountCompleted("task1");
        }

        private void OnScheduled(string task, int worker)
        {
            switch (task)
            {
                case "task1":
                {
                    _task1ScheduledOn = worker;
                    break;
                }
                case "task2":
                {
                    _task2ScheduledOn = worker;
                    break;
                }
                default:
                    Assert.Inconclusive();
                    break;
            }
        }

        [Test]
        public void schedules_on_least_loaded_worker()
        {
            var scheduledOn = -1;
            _balancer.ScheduleTask("task3", (s, on) => { scheduledOn = @on; });

            Assert.AreEqual(_task1ScheduledOn, scheduledOn);
        }

    }
}
