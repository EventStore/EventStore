#nullable enable
using System;
using System.Linq;
using EventStore.Common.Utils;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.UserManagement;

namespace EventStore.Core.Services.PersistentSubscription {
	public class PersistentSubscriptionCheckpointReader : IPersistentSubscriptionCheckpointReader {
		private readonly IODispatcher _ioDispatcher;

		public PersistentSubscriptionCheckpointReader(IODispatcher ioDispatcher) {
			_ioDispatcher = ioDispatcher;
		}

		public void BeginLoadState(string subscriptionId, Action<string?> onStateLoaded) {
			var subscriptionStateStream = "$persistentsubscription-" + subscriptionId + "-checkpoint";
			_ioDispatcher.ReadBackward(subscriptionStateStream, -1, 1, false, SystemAccounts.System,
				new ResponseHandler(onStateLoaded).LoadStateCompleted,
				() => BeginLoadState(subscriptionId, onStateLoaded), Guid.NewGuid());
		}

		private class ResponseHandler {
			private readonly Action<string?> _onStateLoaded;

			public ResponseHandler(Action<string?> onStateLoaded) {
				_onStateLoaded = onStateLoaded;
			}

			public void LoadStateCompleted(ClientMessage.ReadStreamEventsBackwardCompleted msg) {
				if (msg.Events.Length > 0) {
					var checkpoint = msg.Events.Where(v => v.Event.EventType == "$SubscriptionCheckpoint")
						.Select(x => x.Event).FirstOrDefault();
					if (checkpoint != null) {
						string lastEvent = checkpoint.Data.ParseJson<string>();
						_onStateLoaded(lastEvent);
						return;
					}
				}

				_onStateLoaded(null);
			}
		}
	}
}
