using System;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.UserManagement;

namespace EventStore.Core.Services.PersistentSubscription {
	public class PersistentSubscriptionCheckpointWriter : IPersistentSubscriptionCheckpointWriter {
		private readonly IODispatcher _ioDispatcher;
		private long _version = ExpectedVersion.Any;
		private bool _outstandingWrite;
		private readonly string _subscriptionStateStream;
		private static readonly ILogger Log = LogManager.GetLoggerFor<PersistentSubscriptionCheckpointWriter>();

		public PersistentSubscriptionCheckpointWriter(string subscriptionId, IODispatcher ioDispatcher) {
			_subscriptionStateStream = "$persistentsubscription-" + subscriptionId + "-checkpoint";
			_ioDispatcher = ioDispatcher;
		}

		public void StartFrom(long version) {
			_version = version;
		}

		public void BeginWriteState(long state) {
			if (_outstandingWrite) {
				return;
			}

			if (_version == ExpectedVersion.NoStream) {
				PublishMetadata(state);
			} else {
				PublishCheckpoint(state);
			}
		}

		public void BeginDelete(Action<IPersistentSubscriptionCheckpointWriter> completed) {
			_ioDispatcher.DeleteStream(_subscriptionStateStream, ExpectedVersion.Any, false, SystemAccount.Principal,
				x => completed(this));
		}

		private void PublishCheckpoint(long state) {
			_outstandingWrite = true;
			var evnt = new Event(Guid.NewGuid(), "SubscriptionCheckpoint", true, state.ToJson(), null);
			_ioDispatcher.WriteEvent(_subscriptionStateStream, _version, evnt, SystemAccount.Principal,
				WriteStateCompleted);
		}

		private void PublishMetadata(long state) {
			_outstandingWrite = true;
			var metaStreamId = SystemStreams.MetastreamOf(_subscriptionStateStream);
			_ioDispatcher.WriteEvent(
				metaStreamId, ExpectedVersion.Any, CreateStreamMetadataEvent(), SystemAccount.Principal, msg => {
					_outstandingWrite = false;
					switch (msg.Result) {
						case OperationResult.Success:
							PublishCheckpoint(state);
							break;
					}
				});
		}

		private Event CreateStreamMetadataEvent() {
			var eventId = Guid.NewGuid();
			var acl = new StreamAcl(
				readRole: SystemRoles.Admins, writeRole: SystemRoles.Admins,
				deleteRole: SystemRoles.Admins, metaReadRole: SystemRoles.All,
				metaWriteRole: SystemRoles.Admins);
			var metadata = new StreamMetadata(maxCount: 2, maxAge: null, cacheControl: null, acl: acl);
			var dataBytes = metadata.ToJsonBytes();
			return new Event(eventId, SystemEventTypes.StreamMetadata, isJson: true, data: dataBytes, metadata: null);
		}

		private void WriteStateCompleted(ClientMessage.WriteEventsCompleted msg) {
			_outstandingWrite = false;
			if (msg.Result == OperationResult.Success) {
				_version = msg.LastEventNumber;
			} else {
				Log.Debug("Error writing checkpoint for {stream}: {e}", _subscriptionStateStream, msg.Result);
				_version = ExpectedVersion.Any;
			}
		}
	}
}
