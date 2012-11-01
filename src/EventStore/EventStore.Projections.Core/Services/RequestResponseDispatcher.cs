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

namespace EventStore.Projections.Core.Services
{
    public class RequestResponseDispatcher<TRequest, TResponse> : IHandle<TResponse>
        where TRequest : Message where TResponse : Message
    {
        //NOTE: this class is not intened to be used from multiple threads, 
        //however we support count requests from other threads for statistics purposes
        private readonly Dictionary<Guid, Action<TResponse>> _map = new Dictionary<Guid, Action<TResponse>>();
        private readonly IPublisher _publisher;
        private readonly Func<TRequest, Guid> _getRequestCorrelationId;
        private readonly Func<TResponse, Guid> _getResponseCorrelationId;
        private readonly IEnvelope _defaultReplyEnvelope;

        public RequestResponseDispatcher(
            IPublisher publisher, Func<TRequest, Guid> getRequestCorrelationId,
            Func<TResponse, Guid> getResponseCorrelationId, IEnvelope defaultReplyEnvelope)
        {
            _publisher = publisher;
            _getRequestCorrelationId = getRequestCorrelationId;
            _getResponseCorrelationId = getResponseCorrelationId;
            _defaultReplyEnvelope = defaultReplyEnvelope;
        }

        public Guid Publish(TRequest request, Action<TResponse> action)
        {
            //TODO: expiration?
            Guid requestCorrelationId;
            lock (_map)
            {
                requestCorrelationId = _getRequestCorrelationId(request);
                _map.Add(requestCorrelationId, action);
            }
            _publisher.Publish(request);
            return requestCorrelationId;
        }

        void IHandle<TResponse>.Handle(TResponse message)
        {
            Handle(message);
        }

        public bool Handle(TResponse message)
        {
            var correlationId = _getResponseCorrelationId(message);
            Action<TResponse> action;
            lock (_map)
                if (_map.TryGetValue(correlationId, out action))
                {
                    _map.Remove(correlationId);
                    action(message);
                    return true;
                }
            return false;
        }

        public IEnvelope Envelope
        {
            get { return _defaultReplyEnvelope; }
        }

        public void Cancel(Guid requestId)
        {
            lock (_map)
                _map.Remove(requestId);
        }
    }
}
