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

using System;
using EventStore.ClientAPI.ClientOperations;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Represents a subscription to a single stream or to the stream
    /// of all events in the Event Store.
    /// </summary>
    public class EventStoreSubscription : IDisposable
    {
        /// <summary>
        /// True if this subscription is to all streams.
        /// </summary>
        public bool IsSubscribedToAll { get { return _streamId == string.Empty; } }
        /// <summary>
        /// The name of the stream to which the subscription is subscribed.
        /// </summary>
        public string StreamId { get { return _streamId; } }
        /// <summary>
        /// The last commit position seen on the subscription (if this is
        /// a subscription to all events).
        /// </summary>
        public readonly long LastCommitPosition;
        /// <summary>
        /// The last event number seen on the subscription (if this is a
        /// subscription to a single stream).
        /// </summary>
        public readonly int? LastEventNumber;

        private readonly SubscriptionOperation _subscriptionOperation;
        private readonly string _streamId;

        internal EventStoreSubscription(SubscriptionOperation subscriptionOperation, string streamId, long lastCommitPosition, int? lastEventNumber)
        {
            Ensure.NotNull(subscriptionOperation, "subscriptionOperation");

            _subscriptionOperation = subscriptionOperation;
            _streamId = streamId;
            LastCommitPosition = lastCommitPosition;
            LastEventNumber = lastEventNumber;
        }

        /// <summary>
        /// Unsubscribes from the stream.
        /// </summary>
        public void Dispose()
        {
            Unsubscribe();
        }

        /// <summary>
        /// Unsubscribes from the stream.
        /// </summary>
        public void Close()
        {
            Unsubscribe();
        }

        /// <summary>
        /// Unsubscribes from the stream.
        /// </summary>
        public void Unsubscribe()
        {
            _subscriptionOperation.Unsubscribe();
        }
    }
}
