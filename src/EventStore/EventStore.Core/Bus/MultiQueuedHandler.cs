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
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Messaging;

namespace EventStore.Core.Bus
{
    public class MultiQueuedHandler: IHandle<Message>, IPublisher, IThreadSafePublisher
    {
        public readonly QueuedHandler[] Queues;

        private readonly Func<Message, int> _queueHash;
        private int _nextQueueNum = -1;

        public MultiQueuedHandler(int queueCount, 
                                  Func<int, QueuedHandler> queueFactory, 
                                  Func<Message, int> queueHash = null)
        {
            Ensure.Positive(queueCount, "queueCount");
            Ensure.NotNull(queueFactory, "queueFactory");

            Queues = new QueuedHandler[queueCount];
            for (int i = 0; i < Queues.Length; ++i)
            {
                Queues[i] = queueFactory(i);
            }
            _queueHash = queueHash ?? NextQueueHash;
        }

        public MultiQueuedHandler(params QueuedHandler[] queues): this(queues, null)
        {
            Ensure.Positive(queues.Length, "queues.Length");
        }

        public MultiQueuedHandler(QueuedHandler[] queues, Func<Message, int> queueHash)
        {
            Ensure.NotNull(queues, "queues");
            Ensure.Positive(queues.Length, "queues.Length");

            Queues = queues;
            _queueHash = queueHash ?? NextQueueHash;
        }

        private int NextQueueHash(Message msg)
        {
            return Interlocked.Increment(ref _nextQueueNum);
        }

        public void Start()
        {
            for (int i = 0; i < Queues.Length; ++i)
            {
                Queues[i].Start();
            }
        }

        public void Stop()
        {
            var stopTasks = new Task[Queues.Length];
            for (int i = 0; i < Queues.Length; ++i)
            {
                int queueNum = i;
                stopTasks[i] = Task.Factory.StartNew(() => Queues[queueNum].Stop());
            }
            Task.WaitAll(stopTasks);
        }

        public void Handle(Message message)
        {
            Publish(message);
        }

        public void Publish(Message message)
        {
            var queueHash = _queueHash(message);
            var queueNum = (int) ((uint)queueHash % Queues.Length);
            Queues[queueNum].Handle(message);
        }

        public void PublishToAll(Message message)
        {
            for (int i = 0; i < Queues.Length; ++i)
            {
                Queues[i].Publish(message);
            }
        }
    }
}