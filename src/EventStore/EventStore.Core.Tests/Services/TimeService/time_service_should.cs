﻿// Copyright (c) 2012, Event Store LLP
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
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.TimeService
{
    public class FakeScheduler : TimerBasedScheduler
    {
        public FakeScheduler(ITimer timer, ITimeProvider timeProvider): base(timer, timeProvider)
        {
        }

        public void TriggerProcessing()
        {
            ProcessOperations();
        }
    }

    public class FakeTimeProvider : ITimeProvider
    {
        public DateTime Now { get; private set; }

        public FakeTimeProvider()
        {
            Now = DateTime.UtcNow;
        }

        public void SetNewTime(DateTime newTime)
        {
            Now = newTime;
        }

        public void AddTime(TimeSpan timeSpan)
        {
            Now = Now.Add(timeSpan);
        }
    }

    public class FakeTimer : ITimer
    {
        public void FireIn(int milliseconds, Action callback)
        {
            // do smth
        }

        public void Dispose()
        {
        }
    }

    public class TestResponseMessage : Message
    {
        private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId { get { return TypeId; } }

        public int Id { get; set; }

        public TestResponseMessage(int id)
        {
            Id = id;
        }
    }

    [TestFixture]
    public class time_service_should: IHandle<TestResponseMessage>
    {
        private Action<int, int> _startTimeout;
        private List<TestResponseMessage> _timerMessages;

        private FakeTimeProvider _timeProvider;
        private FakeScheduler _scheduler;

        [SetUp]
        public void SetUp()
        {
            _timerMessages = new List<TestResponseMessage>();

            _timeProvider = new FakeTimeProvider();
            var timer = new FakeTimer();
            _scheduler = new FakeScheduler(timer, _timeProvider);
            var timeService = new TimerService(_scheduler);

            _startTimeout = (ms, id) => timeService.Handle(
                TimerMessage.Schedule.Create(TimeSpan.FromMilliseconds(ms),
                                             new SendToThisEnvelope(this),
                                             new TestResponseMessage(id)));
        }

        public void Handle(TestResponseMessage message)
        {
            _timerMessages.Add(message);
        }

        [TearDown]
        public void TearDown()
        {
        }

        [Test]
        public void respond_with_correct_message()
        {
            const int id = 101;

            _startTimeout(0, id);
            _scheduler.TriggerProcessing();

            Assert.That(_timerMessages.ContainsSingle<TestResponseMessage>(msg => msg != null && msg.Id == id));
        }

        [Test]
        public void respond_even_if_fired_too_late()
        {
            _startTimeout(-5, 100);
            _scheduler.TriggerProcessing();

            Assert.That(_timerMessages.ContainsSingle<TestResponseMessage>());
        }

        [Test]
        public void not_respond_until_time_elapses()
        {
            _startTimeout(5, 100);

            _timeProvider.AddTime(TimeSpan.FromMilliseconds(4));
            _scheduler.TriggerProcessing();

            Assert.That(_timerMessages.ContainsNo<TestResponseMessage>());
        }

        [Test]
        public void respond_in_correct_time()
        {
            _startTimeout(5, 100);

            _timeProvider.AddTime(TimeSpan.FromMilliseconds(5));
            _scheduler.TriggerProcessing();

            Assert.That(_timerMessages.ContainsSingle<TestResponseMessage>());
        }

        [Test]
        public void fire_timeouts_gradually()
        {
            _startTimeout(5, 100);
            _startTimeout(25, 102);
            _startTimeout(45, 104);
            _startTimeout(35, 103);
            _startTimeout(15, 101);

            _timeProvider.AddTime(TimeSpan.FromMilliseconds(10)); // 10
            _scheduler.TriggerProcessing();

            Assert.That(_timerMessages.ContainsSingle<TestResponseMessage>(msg => msg.Id == 100) &&
                        _timerMessages.ContainsNo<TestResponseMessage>(msg => msg.Id.IsBetween(101, 104)));

            _timeProvider.AddTime(TimeSpan.FromMilliseconds(10)); // 20
            _scheduler.TriggerProcessing();

            Assert.That(_timerMessages.ContainsSingle<TestResponseMessage>(msg => msg.Id == 101) &&
                        _timerMessages.ContainsNo<TestResponseMessage>(msg => msg.Id.IsBetween(102, 104)));

            _timeProvider.AddTime(TimeSpan.FromMilliseconds(10)); //30
            _scheduler.TriggerProcessing();

            Assert.That(_timerMessages.ContainsSingle<TestResponseMessage>(msg => msg.Id == 102) &&
                        _timerMessages.ContainsNo<TestResponseMessage>(msg => msg.Id.IsBetween(103, 104)));

            _timeProvider.AddTime(TimeSpan.FromMilliseconds(10)); //40
            _scheduler.TriggerProcessing();

            Assert.That(_timerMessages.ContainsSingle<TestResponseMessage>(msg => msg.Id == 103) &&
                        _timerMessages.ContainsNo<TestResponseMessage>(msg => msg.Id == 104));

            _timeProvider.AddTime(TimeSpan.FromMilliseconds(10)); //50
            _scheduler.TriggerProcessing();

            Assert.That(_timerMessages.ContainsSingle<TestResponseMessage>(msg => msg.Id == 104));
        }

        [Test]
        public void fire_all_timeouts_that_are_scheduled_at_same_time()
        {
            _startTimeout(5, 100);
            _startTimeout(5, 101);
            _startTimeout(5, 102);

            _timeProvider.AddTime(TimeSpan.FromMilliseconds(5));
            _scheduler.TriggerProcessing();

            Assert.That(_timerMessages.ContainsN<TestResponseMessage>(3, msg => msg.Id.IsBetween(100, 102)));
        }

        [Test]
        public void fire_all_timeouts_after_long_pause()
        {
            for (int i = 0; i < 20; i++)
                _startTimeout(10 + i, 100 + i);

            Assert.That(_timerMessages.ContainsNo<TestResponseMessage>());

            _timeProvider.AddTime(TimeSpan.FromMilliseconds(1000));
            _scheduler.TriggerProcessing();

            Assert.That(_timerMessages.ContainsN<TestResponseMessage>(20, msg => msg.Id.IsBetween(100, 119)));
        }
    }
}