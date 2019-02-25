using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI {
	interface IHashStream {
		int GetHashFor(string stream);
	}

	class StreamHasher : IHashStream {
		public int GetHashFor(string stream) {
			return stream.GetHashCode();
		}
	}
	public class ShardedConnection : IEventStoreConnection {
		
		private readonly List<IEventStoreConnection> _internalConnections;
		private readonly string _name;
		private int _count;
		private readonly IHashStream _hasher;

		public void Dispose() {
			throw new NotImplementedException();
		}

		public string ConnectionName => _name + " : sharded " + _count + " connections";

		public ShardedConnection(string name, IEnumerable<IEventStoreConnection> connections) {
			_internalConnections = connections.ToList();
			_hasher = new StreamHasher();
			_name = name;
			_count = _internalConnections.Count;
		}

		private IEventStoreConnection GetConnection(string stream) {
			int i = Math.Abs((_hasher.GetHashFor(stream) % _internalConnections.Count));
			return _internalConnections[i];
		}
		
		public Task ConnectAsync() {
			Console.WriteLine("connecting ...");
			return new Task(v => _internalConnections.ForEach(x => x.ConnectAsync()), new object());
		}

		public void Close() {
			_internalConnections.ForEach(x => x.Close());
		}
		public ConnectionSettings Settings { get; }
		
		public Task<DeleteResult> DeleteStreamAsync(string stream, long expectedVersion, UserCredentials userCredentials = null) {
			return GetConnection(stream).DeleteStreamAsync(stream, expectedVersion, userCredentials);
		}

		public Task<DeleteResult> DeleteStreamAsync(string stream, long expectedVersion, bool hardDelete, UserCredentials userCredentials = null) {
			return GetConnection(stream).DeleteStreamAsync(stream, expectedVersion, hardDelete, userCredentials);
		}

		public Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, params EventData[] events) {
			return GetConnection(stream).AppendToStreamAsync(stream, expectedVersion, events);
		}

		public Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, UserCredentials userCredentials,
			params EventData[] events) {
			return GetConnection(stream).AppendToStreamAsync(stream, expectedVersion, events, userCredentials);
		}

		public Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, IEnumerable<EventData> events,
			UserCredentials userCredentials = null) {
			Console.WriteLine("append to " + GetConnection(stream));
			return GetConnection(stream).AppendToStreamAsync(stream, expectedVersion, events, userCredentials);
		}

		public Task<ConditionalWriteResult> ConditionalAppendToStreamAsync(string stream, long expectedVersion, IEnumerable<EventData> events,
			UserCredentials userCredentials = null) {
			return GetConnection(stream)
				.ConditionalAppendToStreamAsync(stream, expectedVersion, events, userCredentials);
		}

		public Task<EventStoreTransaction> StartTransactionAsync(string stream, long expectedVersion, UserCredentials userCredentials = null) {
			return GetConnection(stream).StartTransactionAsync(stream, expectedVersion, userCredentials);
		}

		public EventStoreTransaction ContinueTransaction(long transactionId, UserCredentials userCredentials = null) {
			//TODO revisit how to get connection here?
			return GetConnection("foo").ContinueTransaction(transactionId, userCredentials);
		}

		public Task<EventReadResult> ReadEventAsync(string stream, long eventNumber, bool resolveLinkTos, UserCredentials userCredentials = null) {
			return GetConnection(stream).ReadEventAsync(stream, eventNumber, resolveLinkTos, userCredentials);
		}

		public Task<StreamEventsSlice> ReadStreamEventsForwardAsync(string stream, long start, int count, bool resolveLinkTos,
			UserCredentials userCredentials = null) {
			return GetConnection(stream).ReadStreamEventsForwardAsync(stream, start, count, resolveLinkTos, userCredentials);
		}

		public Task<StreamEventsSlice> ReadStreamEventsBackwardAsync(string stream, long start, int count, bool resolveLinkTos,
			UserCredentials userCredentials = null) {
			return GetConnection(stream).ReadStreamEventsBackwardAsync(stream, start, count, resolveLinkTos, userCredentials);
		}

		public Task<AllEventsSlice> ReadAllEventsForwardAsync(Position position, int maxCount, bool resolveLinkTos,
			UserCredentials userCredentials = null) {
			throw new NotImplementedException();
		}

		public Task<AllEventsSlice> ReadAllEventsBackwardAsync(Position position, int maxCount, bool resolveLinkTos,
			UserCredentials userCredentials = null) {
			throw new NotImplementedException();
		}

		public Task<EventStoreSubscription> SubscribeToStreamAsync(string stream, bool resolveLinkTos, Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared, Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null) {
			return GetConnection(stream).SubscribeToStreamAsync(stream, resolveLinkTos, eventAppeared, subscriptionDropped);
		}

		public EventStoreStreamCatchUpSubscription SubscribeToStreamFrom(string stream, long? lastCheckpoint,
			CatchUpSubscriptionSettings settings, Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared, Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
			Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null, UserCredentials userCredentials = null) {
			return GetConnection(stream).SubscribeToStreamFrom(stream, lastCheckpoint, settings, eventAppeared,
				liveProcessingStarted, subscriptionDropped, userCredentials);
		}

		public Task<EventStoreSubscription> SubscribeToAllAsync(bool resolveLinkTos, Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared, Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null) {
			throw new NotImplementedException();
		}

		public EventStorePersistentSubscriptionBase ConnectToPersistentSubscription(string stream, string groupName,
			Func<EventStorePersistentSubscriptionBase, ResolvedEvent, int?, Task> eventAppeared, Action<EventStorePersistentSubscriptionBase, SubscriptionDropReason, Exception> subscriptionDropped = null, UserCredentials userCredentials = null, int bufferSize = 10,
			bool autoAck = true) {
			return GetConnection(stream).ConnectToPersistentSubscription(stream, groupName, eventAppeared,
				subscriptionDropped, userCredentials, bufferSize);
		}

		public Task<EventStorePersistentSubscriptionBase> ConnectToPersistentSubscriptionAsync(string stream, string groupName, Func<EventStorePersistentSubscriptionBase, ResolvedEvent, int?, Task> eventAppeared,
			Action<EventStorePersistentSubscriptionBase, SubscriptionDropReason, Exception> subscriptionDropped = null, UserCredentials userCredentials = null, int bufferSize = 10,
			bool autoAck = true) {
			return GetConnection(stream).ConnectToPersistentSubscriptionAsync(stream, groupName, eventAppeared,
				subscriptionDropped, userCredentials);
		}

		public EventStoreAllCatchUpSubscription SubscribeToAllFrom(Position? lastCheckpoint, CatchUpSubscriptionSettings settings,
			Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared, Action<EventStoreCatchUpSubscription> liveProcessingStarted = null, Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null) {
			throw new NotImplementedException();
		}

		public Task UpdatePersistentSubscriptionAsync(string stream, string groupName, PersistentSubscriptionSettings settings,
			UserCredentials credentials) {
			return GetConnection(stream).UpdatePersistentSubscriptionAsync(stream, groupName, settings, credentials);
		}

		public Task CreatePersistentSubscriptionAsync(string stream, string groupName, PersistentSubscriptionSettings settings,
			UserCredentials credentials) {
			return GetConnection(stream).CreatePersistentSubscriptionAsync(stream, groupName, settings, credentials);
		}

		public Task DeletePersistentSubscriptionAsync(string stream, string groupName, UserCredentials userCredentials = null) {
			return GetConnection(stream).DeletePersistentSubscriptionAsync(stream, groupName, userCredentials);
		}

		public Task<WriteResult> SetStreamMetadataAsync(string stream, long expectedMetastreamVersion, StreamMetadata metadata,
			UserCredentials userCredentials = null) {
			return GetConnection(stream)
				.SetStreamMetadataAsync(stream, expectedMetastreamVersion, metadata, userCredentials);
		}

		public Task<WriteResult> SetStreamMetadataAsync(string stream, long expectedMetastreamVersion, byte[] metadata,
			UserCredentials userCredentials = null) {
			return GetConnection(stream)
				.SetStreamMetadataAsync(stream, expectedMetastreamVersion, metadata, userCredentials);
		}

		public Task<StreamMetadataResult> GetStreamMetadataAsync(string stream, UserCredentials userCredentials = null) {
			return GetConnection(stream).GetStreamMetadataAsync(stream, userCredentials);
		}

		public Task<RawStreamMetadataResult> GetStreamMetadataAsRawBytesAsync(string stream, UserCredentials userCredentials = null) {
			return GetConnection(stream).GetStreamMetadataAsRawBytesAsync(stream, userCredentials);
		}

		public Task SetSystemSettingsAsync(SystemSettings settings, UserCredentials userCredentials = null) {
			return SetSystemSettingsAsync(settings, userCredentials);
		}

		public event EventHandler<ClientConnectionEventArgs> Connected {
			add {
				foreach (var c in _internalConnections) {
					c.Connected += value;
				} }
			remove {
				foreach (var c in _internalConnections) {
					c.Connected -= value;
				} }
		}

		private event EventHandler<ClientConnectionEventArgs> Disconnected;

		event EventHandler<ClientConnectionEventArgs> IEventStoreConnection.Disconnected {
			add {
				foreach (var c in _internalConnections) {
					c.Disconnected += value;
				}
			}
			remove {
				foreach (var c in _internalConnections) {
					c.Disconnected -= value;
				}
			}
		}

		public event EventHandler<ClientReconnectingEventArgs> Reconnecting {
			add {
				foreach (var c in _internalConnections) {
					c.Reconnecting += value;
				}
			}
			remove {
				foreach (var c in _internalConnections) {
					c.Reconnecting -= value;
				} 
			}
		}

		private event EventHandler<ClientClosedEventArgs> Closed {
			add {
				foreach (var c in _internalConnections) {
					c.Closed += value;
				}
			}
			remove {
				foreach (var c in _internalConnections) {
					c.Closed -= value;
				}
			}
		}

		event EventHandler<ClientClosedEventArgs> IEventStoreConnection.Closed {
			add {
				foreach (var c in _internalConnections) {
					c.Closed += value;
				}
			}
			remove {
				foreach (var c in _internalConnections) {
					c.Closed -= value;
				}
			}
		}

		public event EventHandler<ClientErrorEventArgs> ErrorOccurred {
			add {
				foreach (var c in _internalConnections) {
					c.ErrorOccurred += value;
				}
			}
			remove {
				foreach (var c in _internalConnections) {
					c.ErrorOccurred -= value;
				}
			}
		}

		public event EventHandler<ClientAuthenticationFailedEventArgs> AuthenticationFailed {
			add {
				foreach (var c in _internalConnections) {
					c.AuthenticationFailed += value;
				}
			}
			remove {
				foreach (var c in _internalConnections) {
					c.AuthenticationFailed -= value;
				} }
		}
	}
}
