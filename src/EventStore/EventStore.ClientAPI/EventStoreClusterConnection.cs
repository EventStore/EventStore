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
using System.Threading.Tasks;
using EventStore.ClientAPI.Core;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Maintains a full duplex connection to the EventStore cluster
    /// </summary>
    internal class EventStoreClusterConnection: IEventStoreConnection, IEventStoreTransactionConnection
    {
        public string ConnectionName { get { return _conn.ConnectionName; } }

        private readonly EventStoreNodeConnection _conn;

        internal EventStoreClusterConnection(ConnectionSettings connectionSettings,
                                             ClusterSettings clusterSettings,
                                             string connectionName)
        {

            var endPointDiscoverer = new ClusterDnsEndPointDiscoverer(connectionSettings.Log,
                                                                      clusterSettings.ClusterDns,
                                                                      clusterSettings.MaxDiscoverAttempts,
                                                                      clusterSettings.ManagerExternalHttpPort);
            _conn = new EventStoreNodeConnection(connectionSettings, endPointDiscoverer, connectionName);
        }

        public void Connect()
        {
            _conn.Connect();
        }

        public Task ConnectAsync()
        {
            return _conn.ConnectAsync();
        }

        public void Dispose()
        {
            Close();
        }

        public void Close()
        {
            _conn.Close();
        }

        public void DeleteStream(string stream, int expectedVersion)
        {
            _conn.DeleteStream(stream, expectedVersion);
        }

        public Task DeleteStreamAsync(string stream, int expectedVersion)
        {
            return _conn.DeleteStreamAsync(stream, expectedVersion);
        }

        public void AppendToStream(string stream, int expectedVersion, params EventData[] events)
        {
            _conn.AppendToStream(stream, expectedVersion, events);
        }

        public void AppendToStream(string stream, int expectedVersion, IEnumerable<EventData> events)
        {
            _conn.AppendToStream(stream, expectedVersion, events);
        }

        public Task AppendToStreamAsync(string stream, int expectedVersion, params EventData[] events)
        {
            return _conn.AppendToStreamAsync(stream, expectedVersion, events);
        }

        public Task AppendToStreamAsync(string stream, int expectedVersion, IEnumerable<EventData> events)
        {
            return _conn.AppendToStreamAsync(stream, expectedVersion, events);
        }

        public EventStoreTransaction StartTransaction(string stream, int expectedVersion)
        {
            return _conn.StartTransaction(stream, expectedVersion);
        }

        public Task<EventStoreTransaction> StartTransactionAsync(string stream, int expectedVersion)
        {
            return _conn.StartTransactionAsync(stream, expectedVersion);
        }

        public EventStoreTransaction ContinueTransaction(long transactionId)
        {
            return _conn.ContinueTransaction(transactionId);
        }

        public Task TransactionalWriteAsync(EventStoreTransaction transaction, IEnumerable<EventData> events)
        {
            return ((IEventStoreTransactionConnection)_conn).TransactionalWriteAsync(transaction, events);
        }

        public Task CommitTransactionAsync(EventStoreTransaction transaction)
        {
            return ((IEventStoreTransactionConnection)_conn).CommitTransactionAsync(transaction);
        }

        public StreamEventsSlice ReadStreamEventsForward(string stream, int start, int count, bool resolveLinkTos)
        {
            return _conn.ReadStreamEventsForward(stream, start, count, resolveLinkTos);
        }

        public Task<StreamEventsSlice> ReadStreamEventsForwardAsync(string stream, int start, int count, bool resolveLinkTos)
        {
            return _conn.ReadStreamEventsForwardAsync(stream, start, count, resolveLinkTos);
        }

        public StreamEventsSlice ReadStreamEventsBackward(string stream, int start, int count, bool resolveLinkTos)
        {
            return _conn.ReadStreamEventsBackward(stream, start, count, resolveLinkTos);
        }

        public Task<StreamEventsSlice> ReadStreamEventsBackwardAsync(string stream, int start, int count, bool resolveLinkTos)
        {
            return _conn.ReadStreamEventsBackwardAsync(stream, start, count, resolveLinkTos);
        }

        public AllEventsSlice ReadAllEventsForward(Position position, int maxCount, bool resolveLinkTos)
        {
            return _conn.ReadAllEventsForward(position, maxCount, resolveLinkTos);
        }

        public Task<AllEventsSlice> ReadAllEventsForwardAsync(Position position, int maxCount, bool resolveLinkTos)
        {
            return _conn.ReadAllEventsForwardAsync(position, maxCount, resolveLinkTos);
        }

        public AllEventsSlice ReadAllEventsBackward(Position position, int maxCount, bool resolveLinkTos)
        {
            return _conn.ReadAllEventsBackward(position, maxCount, resolveLinkTos);
        }

        public Task<AllEventsSlice> ReadAllEventsBackwardAsync(Position position, int maxCount, bool resolveLinkTos)
        {
            return _conn.ReadAllEventsBackwardAsync(position, maxCount, resolveLinkTos);
        }

        public Task<EventStoreSubscription> SubscribeToStream(string stream, 
                                                              bool resolveLinkTos, 
                                                              Action<EventStoreSubscription, ResolvedEvent> eventAppeared, 
                                                              Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null)
        {
            return _conn.SubscribeToStream(stream, resolveLinkTos, eventAppeared, subscriptionDropped);
        }

        public EventStoreStreamCatchUpSubscription SubscribeToStreamFrom(string stream,
                                                                         int? fromEventNumberExclusive,
                                                                         bool resolveLinkTos,
                                                                         Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                                                                         Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
                                                                         Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null)
        {
            return _conn.SubscribeToStreamFrom(stream, fromEventNumberExclusive, resolveLinkTos, 
                                               eventAppeared, liveProcessingStarted, subscriptionDropped);
        }

        public Task<EventStoreSubscription> SubscribeToAll(bool resolveLinkTos, 
                                                           Action<EventStoreSubscription, ResolvedEvent> eventAppeared, 
                                                           Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null)
        {
            return _conn.SubscribeToAll(resolveLinkTos, eventAppeared, subscriptionDropped);
        }

        public EventStoreAllCatchUpSubscription SubscribeToAllFrom(Position? fromPositionExclusive,
                                                                   bool resolveLinkTos,
                                                                   Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                                                                   Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
                                                                   Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null)
        {
            return _conn.SubscribeToAllFrom(fromPositionExclusive, resolveLinkTos, 
                                            eventAppeared, liveProcessingStarted, subscriptionDropped);
        }

        public void SetStreamMetadata(string stream, int expectedMetastreamVersion, Guid idempotencyId, StreamMetadata metadata)
        {
            _conn.SetStreamMetadata(stream, expectedMetastreamVersion, idempotencyId, metadata);
        }

        public Task SetStreamMetadataAsync(string stream, int expectedMetastreamVersion, Guid idempotencyId, StreamMetadata metadata)
        {
            return _conn.SetStreamMetadataAsync(stream, expectedMetastreamVersion, idempotencyId, metadata);
        }

        public void SetStreamMetadata(string stream, int expectedMetastreamVersion, Guid idempotencyId, byte[] metadata)
        {
            _conn.SetStreamMetadata(stream, expectedMetastreamVersion, idempotencyId, metadata);
        }

        public Task SetStreamMetadataAsync(string stream, int expectedMetastreamVersion, Guid idempotencyId, byte[] metadata)
        {
            return _conn.SetStreamMetadataAsync(stream, expectedMetastreamVersion, idempotencyId, metadata);
        }

        public StreamMetadataResult GetStreamMetadata(string stream)
        {
            return _conn.GetStreamMetadata(stream);
        }

        public Task<StreamMetadataResult> GetStreamMetadataAsync(string stream)
        {
            return _conn.GetStreamMetadataAsync(stream);
        }

        public RawStreamMetadataResult GetStreamMetadataAsRawBytes(string stream)
        {
            return _conn.GetStreamMetadataAsRawBytes(stream);
        }

        public Task<RawStreamMetadataResult> GetStreamMetadataAsRawBytesAsync(string stream)
        {
            return _conn.GetStreamMetadataAsRawBytesAsync(stream);
        }
    }
}