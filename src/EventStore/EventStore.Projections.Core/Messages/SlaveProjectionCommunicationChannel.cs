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
using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Messages
{
    public sealed class SlaveProjectionCommunicationChannel
    {
        private readonly Guid _coreProjectionId;
        private readonly Guid _subscriptionId;
        private readonly IPublisher _publishEnvelope;
        private readonly string _managedProjectionName;

        public SlaveProjectionCommunicationChannel(
            string managedProjectionName, Guid coreProjectionId, Guid subscriptionId, IPublisher publishEnvelope)
        {
            _managedProjectionName = managedProjectionName;
            _coreProjectionId = coreProjectionId;
            _subscriptionId = subscriptionId;
            _publishEnvelope = publishEnvelope;
        }

        public Guid CoreProjectionId
        {
            get { return _coreProjectionId; }
        }

        public IPublisher PublishEnvelope
        {
            get { return _publishEnvelope; }
        }

        public Guid SubscriptionId
        {
            get { return _subscriptionId; }
        }

        public string ManagedProjectionName
        {
            get { return _managedProjectionName; }
        }
    }
}
