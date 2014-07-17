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
using System.Linq;
using System.Threading.Tasks;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Tcp;

namespace EventStore.ClientAPI.ClientOperations
{
    internal class PersistentSubscriptionOperation : SubscriptionOperation<PersistentEventStoreSubscription>
    {
        private readonly string _subscriptionId;
        private readonly int _bufferSize;

        public PersistentSubscriptionOperation(ILogger log, TaskCompletionSource<PersistentEventStoreSubscription> source, string subscriptionId, int bufferSize, string streamId, bool resolveLinkTos, UserCredentials userCredentials, Action<PersistentEventStoreSubscription, ResolvedEvent> eventAppeared, Action<PersistentEventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped, bool verboseLogging, Func<TcpPackageConnection> getConnection)
            : base(log, source, streamId, resolveLinkTos, userCredentials, eventAppeared, subscriptionDropped, verboseLogging, getConnection)
        {
            _subscriptionId = subscriptionId;
            _bufferSize = bufferSize;
        }

        protected override TcpPackage CreateSubscriptionPackage()
        {
            var dto = new ClientMessage.ConnectToPersistentSubscription(_subscriptionId, _streamId, _resolveLinkTos, _bufferSize);
            return new TcpPackage(TcpCommand.ConnectToPersistentSubscription,
                                  _userCredentials != null ? TcpFlags.Authenticated : TcpFlags.None,
                                  _correlationId,
                                  _userCredentials != null ? _userCredentials.Username : null,
                                  _userCredentials != null ? _userCredentials.Password : null,
                                  dto.Serialize());
        }

        protected override bool InspectPackage(TcpPackage package, out InspectionResult result)
        {
            if (package.Command == TcpCommand.PersistentSubscriptionConfirmation)
            {
                var dto = package.Data.Deserialize<ClientMessage.PersistentSubscriptionConfirmation>();
                        ConfirmSubscription(dto.LastCommitPosition, dto.LastEventNumber);
                        result = new InspectionResult(InspectionDecision.Subscribed, "SubscriptionConfirmation");
                return true;
            }
            if (package.Command == TcpCommand.PersistentSubscriptionStreamEventAppeared)
            {
                var dto = package.Data.Deserialize<ClientMessage.PersistentSubscriptionStreamEventAppeared>();
                EventAppeared(new ResolvedEvent(dto.Event));
                result = new InspectionResult(InspectionDecision.DoNothing, "StreamEventAppeared");
                return true;
            }
            result = null;
            return false;
        }

        protected override PersistentEventStoreSubscription CreateSubscriptionObject(long lastCommitPosition, int? lastEventNumber)
        {
            return new PersistentEventStoreSubscription(this, _streamId, lastCommitPosition, lastEventNumber);
        }

        public void NotifyEventsProcessed(int freeSlots, Guid[] processedEvents)
        {
            var dto = new ClientMessage.PersistentSubscriptionNotifyEventsProcessed(
                _subscriptionId, freeSlots,
                processedEvents.Select(x => x.ToByteArray()).ToArray());

            var package = new TcpPackage(TcpCommand.PersistentSubscriptionNotifyEventsProcessed,
                                  _userCredentials != null ? TcpFlags.Authenticated : TcpFlags.None,
                                  _correlationId,
                                  _userCredentials != null ? _userCredentials.Username : null,
                                  _userCredentials != null ? _userCredentials.Password : null,
                                  dto.Serialize());
            EnqueueSend(package);
        }
    }
}