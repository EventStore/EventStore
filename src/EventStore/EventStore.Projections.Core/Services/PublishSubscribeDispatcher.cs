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
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Services
{
    public class PublishSubscribeDispatcher<TSubscribeRequest, TControlMessageBase, TResponseBase> 
        where TSubscribeRequest : Message 
        where TControlMessageBase: Message
        where TResponseBase : Message
    {
        //NOTE: this class is not intended to be used from multiple threads, 
        //however we support count requests from other threads for statistics purposes

        private readonly Dictionary<Guid, object> _map = new Dictionary<Guid, object>();
        private readonly IPublisher _publisher;
        private readonly Func<TSubscribeRequest, Guid> _getRequestCorrelationId;
        private readonly Func<TResponseBase, Guid> _getResponseCorrelationId;

        public PublishSubscribeDispatcher(
            IPublisher publisher, Func<TSubscribeRequest, Guid> getRequestCorrelationId,
            Func<TResponseBase, Guid> getResponseCorrelationId)
        {
            _publisher = publisher;
            _getRequestCorrelationId = getRequestCorrelationId;
            _getResponseCorrelationId = getResponseCorrelationId;
        }

        public Guid PublishSubscribe(TSubscribeRequest request, object subscriber)
        {
            return PublishSubscribe(_publisher, request, subscriber);
        }

        public Guid PublishSubscribe(IPublisher publisher, TSubscribeRequest request, object subscriber)
        {
//TODO: expiration?
            Guid requestCorrelationId;
            lock (_map)
            {
                requestCorrelationId = _getRequestCorrelationId(request);
                _map.Add(requestCorrelationId, subscriber);
            }
            publisher.Publish(request);
            //NOTE: the following condition is required as publishing the message could also process the message 
            // and the correlationId is already invalid here (as subscriber unsubscribed)
            return _map.ContainsKey(requestCorrelationId) ? requestCorrelationId : Guid.Empty;
        }

        public void Publish(TControlMessageBase request)
        {
            Publish(_publisher, request);
        }

        public void Publish(IPublisher publisher, TControlMessageBase request)
        {
            publisher.Publish(request);
        }


        public void Cancel(Guid requestId)
        {
            lock (_map)
                _map.Remove(requestId);
        }

        public void CancelAll()
        {
            lock (_map)
                _map.Clear();
        }

        public IHandle<T> CreateSubscriber<T>() where T : TResponseBase
        {
            return new Subscriber<T>(this);
        }

        private class Subscriber<T> : IHandle<T> where T : TResponseBase
        {
            private readonly PublishSubscribeDispatcher<TSubscribeRequest, TControlMessageBase, TResponseBase> _host;

            public Subscriber(PublishSubscribeDispatcher<TSubscribeRequest, TControlMessageBase, TResponseBase> host)
            {
                _host = host;
            }

            public void Handle(T message)
            {
                _host.Handle<T>(message);
            }
        }

        public bool Handle<T>(T message) where T : TResponseBase
        {
            var correlationId = _getResponseCorrelationId(message);
            lock (_map)
            {
                object subscriber;
                if (_map.TryGetValue(correlationId, out subscriber))
                {
                    var h = subscriber as IHandle<T>;
                    if (h != null)
                        h.Handle(message);
                    return true;
                }
            }
            return false;
        }

        public void Subscribed(Guid correlationId, object subscriber)
        {
            lock (_map)
            {
                _map.Add(correlationId, subscriber);
            }
        }
    }
}
