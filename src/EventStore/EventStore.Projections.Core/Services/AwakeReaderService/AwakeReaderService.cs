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
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.AwakeReaderService
{
    public class AwakeReaderService : IHandle<AwakeReaderServiceMessage.SubscribeAwake>,
        IHandle<AwakeReaderServiceMessage.UnsubscribeAwake>,
        IHandle<StorageMessage.EventCommitted>,
        IHandle<StorageMessage.TfEofAtNonCommitRecord>

    {
        private readonly Dictionary<string, HashSet<AwakeReaderServiceMessage.SubscribeAwake>> _subscribers =
            new Dictionary<string, HashSet<AwakeReaderServiceMessage.SubscribeAwake>>();

        private readonly Dictionary<Guid, AwakeReaderServiceMessage.SubscribeAwake> _map =
            new Dictionary<Guid, AwakeReaderServiceMessage.SubscribeAwake>();

        private TFPos _lastPosition;

        public void Handle(AwakeReaderServiceMessage.SubscribeAwake message)
        {
            if (message.From < _lastPosition)
            {
                message.Envelope.ReplyWith(message.ReplyWithMessage);
                return;
            }
            _map.Add(message.CorrelationId, message);
            HashSet<AwakeReaderServiceMessage.SubscribeAwake> list;
            string streamId = message.StreamId ?? "$all";
            if (!_subscribers.TryGetValue(streamId, out list))
            {
                list = new HashSet<AwakeReaderServiceMessage.SubscribeAwake>();
                _subscribers.Add(streamId, list);
            }
            list.Add(message);
        }

        public void Handle(StorageMessage.EventCommitted message)
        {
            _lastPosition = new TFPos(message.CommitPosition, message.Event.LogPosition);
            NotifyEventInStream("$all", message);
            NotifyEventInStream(message.Event.EventStreamId, message);
        }

        private void NotifyEventInStream(string streamId, StorageMessage.EventCommitted message)
        {
            HashSet<AwakeReaderServiceMessage.SubscribeAwake> list;
            List<AwakeReaderServiceMessage.SubscribeAwake> toRemove = null;
            if (_subscribers.TryGetValue(streamId, out list))
            {
                foreach (var subscriber in list)
                {
                    if (subscriber.From < new TFPos(message.CommitPosition, message.Event.LogPosition))
                    {
                        subscriber.Envelope.ReplyWith(subscriber.ReplyWithMessage);
                        _map.Remove(subscriber.CorrelationId);
                        if (toRemove == null)
                            toRemove = new List<AwakeReaderServiceMessage.SubscribeAwake>();
                        toRemove.Add(subscriber);
                    }
                }
                if (toRemove != null)
                {
                    foreach (var item in toRemove)
                        list.Remove(item);
                    if (list.Count == 0)
                    {
                        _subscribers.Remove(streamId);
                    }
                }
            }
        }

        public void Handle(AwakeReaderServiceMessage.UnsubscribeAwake message)
        {
            AwakeReaderServiceMessage.SubscribeAwake subscriber;
            if (_map.TryGetValue(message.CorrelationId, out subscriber))
            {
                _map.Remove(message.CorrelationId);
                var list = _subscribers[subscriber.StreamId ?? "$all"];
                list.Remove(subscriber);
            }
        }

        public void Handle(StorageMessage.TfEofAtNonCommitRecord message)
        {
            throw new NotImplementedException();
        }
    }
}
