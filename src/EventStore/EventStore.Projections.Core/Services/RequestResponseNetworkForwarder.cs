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
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services
{
    public class RequestResponseNetworkForwarder :
        IHandle<ClientMessage.ReadEvent>,
        IHandle<ClientMessage.ReadEventCompleted>,
        IHandle<ProjectionCoreServiceMessage.Connected>,
        IHandle<ProjectionCoreServiceMessage.StartCore>,
        IHandle<ProjectionCoreServiceMessage.StopCore>
    {
        private TcpConnectionManager _connection;
        private Dictionary<Guid, IEnvelope> _correlations;


        public void Handle(ClientMessage.ReadEvent message)
        {
            _correlations.Add(message.CorrelationId, message.Envelope);
            //TODO: null envelope?
            _connection.SendMessage(message);
        }

        public void Handle(ProjectionCoreServiceMessage.Connected message)
        {
            _connection = message.Connection;
        }

        public void Handle(ProjectionCoreServiceMessage.StopCore message)
        {
            _connection = null;
            _correlations = null;
        }

        public void Handle(ProjectionCoreServiceMessage.StartCore message)
        {
            _correlations = new Dictionary<Guid, IEnvelope>();
        }

        public void Handle(ClientMessage.ReadEventCompleted message)
        {
            IEnvelope envelope;
            if (_correlations.TryGetValue(message.CorrelationId, out envelope))
            {
                envelope.ReplyWith(message);
                _correlations.Remove(message.CorrelationId);
            }
        }
    }
}