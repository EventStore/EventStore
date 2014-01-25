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
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using System.Linq;

namespace EventStore.Core.Tests.Helpers
{
    public class ManualQueue : IPublisher, IHandle<TimerMessage.Schedule>
    {
        private readonly Queue<Message> _queue = new Queue<Message>();
        private readonly IBus _bus;
        private readonly ITimeProvider _time;
        private readonly List<InternalSchedule> _timerQueue = new List<InternalSchedule>();
        private bool _timerDisabled;

        public ManualQueue(IBus bus, ITimeProvider time)
        {
            _bus = bus;
            _time = time;
            _bus.Subscribe(this);
        }

        public void Publish(Message message)
        {
            _queue.Enqueue(message);
        }

        public int Process()
        {
            ProcessTimer();
            return ProcessNonTimer();
        }

        public int ProcessNonTimer()
        {
            var count = 0;
            while (_queue.Count > 0)
            {
                var message = _queue.Dequeue();
                _bus.Publish(message);
                count++;
                if (count > 1000)
                    throw new Exception("Possible infinite message loop");
            }
            return count;
        }

        public void ProcessTimer()
        {
            if (!_timerDisabled)
            {
                var orderedTimerMessages = _timerQueue.OrderBy(v => v.Scheduled.Add(v.Message.TriggerAfter)).ToArray();
                _timerQueue.Clear();
                foreach (var timerMessage in orderedTimerMessages)
                {
                    if (timerMessage.Scheduled.Add(timerMessage.Message.TriggerAfter) <= _time.Now)
                        timerMessage.Message.Reply();
                    else
                        _timerQueue.Add(timerMessage);
                }
            }
        }

        public void DisableTimer()
        {
            _timerDisabled = true;
        }

        public void EnableTimer(bool process = true)
        {
            _timerDisabled = false;
            if (process)
                Process();
        }

        private class InternalSchedule
        {
            public readonly TimerMessage.Schedule Message;
            public readonly DateTime Scheduled;

            public InternalSchedule(TimerMessage.Schedule message, DateTime scheduled)
            {
                Message = message;
                Scheduled = scheduled;
            }
        }

        public void Handle(TimerMessage.Schedule message)
        {
            _timerQueue.Add(new InternalSchedule(message, _time.Now));
        }
    }
}
