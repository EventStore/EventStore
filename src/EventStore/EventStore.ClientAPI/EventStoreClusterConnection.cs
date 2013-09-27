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
using EventStore.ClientAPI.SystemData;

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
                                                                      clusterSettings.ManagerExternalHttpPort,
                                                                      clusterSettings.FakeDnsEntries,
                                                                      clusterSettings.GossipTimeout);
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

        public void DeleteStream(string stream, int expectedVersion, UserCredentials userCredentials = null)
        {
            _conn.DeleteStream(stream, expectedVersion, userCredentials);
        }

        public void DeleteStream(string stream, int expectedVersion, bool hardDelete, UserCredentials userCredentials = null)
        {
            _conn.DeleteStream(stream, expectedVersion, hardDelete, userCredentials);
        }

        public Task DeleteStreamAsync(string stream, int expectedVersion, UserCredentials userCredentials = null)
        {
            return _conn.DeleteStreamAsync(stream, expectedVersion, userCredentials);
        }
        
        public Task DeleteStreamAsync(string stream, int expectedVersion, bool hardDelete, UserCredentials userCredentials = null)
        {
            return _conn.DeleteStreamAsync(stream, expectedVersion, hardDelete, userCredentials);
        }

        public WriteResult AppendToStream(string stream, int expectedVersion, params EventData[] events)
        {
            return _conn.AppendToStream(stream, expectedVersion, events);
        }

        public WriteResult AppendToStream(string stream, int expectedVersion, UserCredentials userCredentials, params EventData[] events)
        {
            return _conn.AppendToStream(stream, expectedVersion, userCredentials, events);
        }

        public WriteResult AppendToStream(string stream, int expectedVersion, IEnumerable<EventData> events, UserCredentials userCredentials = null)
        {
            return _conn.AppendToStream(stream, expectedVersion, events, userCredentials);
        }

        public Task<WriteResult> AppendToStreamAsync(string stream, int expectedVersion, params EventData[] events)
        {
            return _conn.AppendToStreamAsync(stream, expectedVersion, events);
        }

        public Task<WriteResult> AppendToStreamAsync(string stream, int expectedVersion, UserCredentials userCredentials, params EventData[] events)
        {
            return _conn.AppendToStreamAsync(stream, expectedVersion, userCredentials, events);
        }

        public Task<WriteResult> AppendToStreamAsync(string stream, int expectedVersion, IEnumerable<EventData> events, UserCredentials userCredentials = null)
        {
            return _conn.AppendToStreamAsync(stream, expectedVersion, events, userCredentials);
        }

        public EventStoreTransaction StartTransaction(string stream, int expectedVersion, UserCredentials userCredentials = null)
        {
            return _conn.StartTransaction(stream, expectedVersion, userCredentials);
        }

        public Task<EventStoreTransaction> StartTransactionAsync(string stream, int expectedVersion, UserCredentials userCredentials = null)
        {
            return _conn.StartTransactionAsync(stream, expectedVersion, userCredentials);
        }

        public EventStoreTransaction ContinueTransaction(long transactionId, UserCredentials userCredentials = null)
        {
            return _conn.ContinueTransaction(transactionId, userCredentials);
        }

        public EventReadResult ReadEvent(string stream, int eventNumber, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            return _conn.ReadEvent(stream, eventNumber, resolveLinkTos, userCredentials);
        }

        public Task<EventReadResult> ReadEventAsync(string stream, int eventNumber, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            return _conn.ReadEventAsync(stream, eventNumber, resolveLinkTos, userCredentials);
        }

        public Task TransactionalWriteAsync(EventStoreTransaction transaction, IEnumerable<EventData> events, UserCredentials userCredentials = null)
        {
            return ((IEventStoreTransactionConnection)_conn).TransactionalWriteAsync(transaction, events, userCredentials);
        }

        public Task<WriteResult> CommitTransactionAsync(EventStoreTransaction transaction, UserCredentials userCredentials = null)
        {
            return ((IEventStoreTransactionConnection)_conn).CommitTransactionAsync(transaction, userCredentials);
        }

        public StreamEventsSlice ReadStreamEventsForward(string stream, int start, int count, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            return _conn.ReadStreamEventsForward(stream, start, count, resolveLinkTos, userCredentials);
        }

        public Task<StreamEventsSlice> ReadStreamEventsForwardAsync(string stream, int start, int count, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            return _conn.ReadStreamEventsForwardAsync(stream, start, count, resolveLinkTos, userCredentials);
        }

        public StreamEventsSlice ReadStreamEventsBackward(string stream, int start, int count, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            return _conn.ReadStreamEventsBackward(stream, start, count, resolveLinkTos, userCredentials);
        }

        public Task<StreamEventsSlice> ReadStreamEventsBackwardAsync(string stream, int start, int count, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            return _conn.ReadStreamEventsBackwardAsync(stream, start, count, resolveLinkTos, userCredentials);
        }

        public AllEventsSlice ReadAllEventsForward(Position position, int maxCount, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            return _conn.ReadAllEventsForward(position, maxCount, resolveLinkTos, userCredentials);
        }

        public Task<AllEventsSlice> ReadAllEventsForwardAsync(Position position, int maxCount, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            return _conn.ReadAllEventsForwardAsync(position, maxCount, resolveLinkTos, userCredentials);
        }

        public AllEventsSlice ReadAllEventsBackward(Position position, int maxCount, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            return _conn.ReadAllEventsBackward(position, maxCount, resolveLinkTos, userCredentials);
        }

        public Task<AllEventsSlice> ReadAllEventsBackwardAsync(Position position, int maxCount, bool resolveLinkTos, UserCredentials userCredentials = null)
        {
            return _conn.ReadAllEventsBackwardAsync(position, maxCount, resolveLinkTos, userCredentials);
        }

        public EventStoreSubscription SubscribeToStream(
                string stream,
                bool resolveLinkTos,
                Action<EventStoreSubscription, ResolvedEvent> eventAppeared,
                Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                UserCredentials userCredentials = null)
        {
            return _conn.SubscribeToStream(stream, resolveLinkTos, eventAppeared, subscriptionDropped, userCredentials);
        }

        public Task<EventStoreSubscription> SubscribeToStreamAsync(
                string stream, 
                bool resolveLinkTos, 
                Action<EventStoreSubscription, ResolvedEvent> eventAppeared, 
                Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                UserCredentials userCredentials = null)
        {
            return _conn.SubscribeToStreamAsync(stream, resolveLinkTos, eventAppeared, subscriptionDropped, userCredentials);
        }

        public EventStoreStreamCatchUpSubscription SubscribeToStreamFrom(
                string stream,
                int? fromEventNumberExclusive,
                bool resolveLinkTos,
                Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
                Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                UserCredentials userCredentials = null,
                int readBatchSize = 500)
        {
            return _conn.SubscribeToStreamFrom(stream, fromEventNumberExclusive, resolveLinkTos,
                                               eventAppeared, liveProcessingStarted,
                                               subscriptionDropped, userCredentials, readBatchSize);
        }

        public EventStoreSubscription SubscribeToAll(
                bool resolveLinkTos, 
                Action<EventStoreSubscription, ResolvedEvent> eventAppeared, 
                Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                UserCredentials userCredentials = null)
        {
            return _conn.SubscribeToAll(resolveLinkTos, eventAppeared, subscriptionDropped, userCredentials);
        }

        public Task<EventStoreSubscription> SubscribeToAllAsync(
                bool resolveLinkTos,
                Action<EventStoreSubscription, ResolvedEvent> eventAppeared,
                Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                UserCredentials userCredentials = null)
        {
            return _conn.SubscribeToAllAsync(resolveLinkTos, eventAppeared, subscriptionDropped, userCredentials);
        }

        public EventStoreAllCatchUpSubscription SubscribeToAllFrom(
                Position? fromPositionExclusive,
                bool resolveLinkTos,
                Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
                Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                UserCredentials userCredentials = null,
                int readBatchSize = 500)
        {
            return _conn.SubscribeToAllFrom(fromPositionExclusive, resolveLinkTos,
                                            eventAppeared, liveProcessingStarted, subscriptionDropped, userCredentials, readBatchSize);
        }

        public WriteResult SetStreamMetadata(string stream, int expectedMetastreamVersion, StreamMetadata metadata, UserCredentials userCredentials = null)
        {
            return _conn.SetStreamMetadata(stream, expectedMetastreamVersion, metadata, userCredentials);
        }

        public Task<WriteResult> SetStreamMetadataAsync(string stream, int expectedMetastreamVersion, StreamMetadata metadata, UserCredentials userCredentials = null)
        {
            return _conn.SetStreamMetadataAsync(stream, expectedMetastreamVersion, metadata, userCredentials);
        }

        public WriteResult SetStreamMetadata(string stream, int expectedMetastreamVersion, byte[] metadata, UserCredentials userCredentials = null)
        {
            return _conn.SetStreamMetadata(stream, expectedMetastreamVersion, metadata, userCredentials);
        }

        public Task<WriteResult> SetStreamMetadataAsync(string stream, int expectedMetastreamVersion, byte[] metadata, UserCredentials userCredentials = null)
        {
            return _conn.SetStreamMetadataAsync(stream, expectedMetastreamVersion, metadata, userCredentials);
        }

        public StreamMetadataResult GetStreamMetadata(string stream, UserCredentials userCredentials = null)
        {
            return _conn.GetStreamMetadata(stream, userCredentials);
        }

        public Task<StreamMetadataResult> GetStreamMetadataAsync(string stream, UserCredentials userCredentials = null)
        {
            return _conn.GetStreamMetadataAsync(stream, userCredentials);
        }

        public RawStreamMetadataResult GetStreamMetadataAsRawBytes(string stream, UserCredentials userCredentials = null)
        {
            return _conn.GetStreamMetadataAsRawBytes(stream, userCredentials);
        }

        public Task<RawStreamMetadataResult> GetStreamMetadataAsRawBytesAsync(string stream, UserCredentials userCredentials = null)
        {
            return _conn.GetStreamMetadataAsRawBytesAsync(stream, userCredentials);
        }

        public void SetSystemSettings(SystemSettings settings, UserCredentials userCredentials = null)
        {
            _conn.SetSystemSettings(settings, userCredentials);
        }

        public Task SetSystemSettingsAsync(SystemSettings settings, UserCredentials userCredentials = null)
        {
            return _conn.SetSystemSettingsAsync(settings, userCredentials);
        }
    }
}