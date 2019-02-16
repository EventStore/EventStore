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

		public void BeginLoadState(string subscriptionId, Action<long?> onStateLoaded) {
			var subscriptionStateStream = "$persistentsubscription-" + subscriptionId + "-checkpoint";
			_ioDispatcher.ReadBackward(subscriptionStateStream, -1, 1, false, SystemAccount.Principal,
				new ResponseHandler(onStateLoaded).LoadStateCompleted);
		}

		private class ResponseHandler {
			private readonly Action<long?> _onStateLoaded;

			public ResponseHandler(Action<long?> onStateLoaded) {
				_onStateLoaded = onStateLoaded;
			}

			public void LoadStateCompleted(ClientMessage.ReadStreamEventsBackwardCompleted msg) {
				if (msg.Events.Length > 0) {
					var checkpoint = msg.Events.Where(v => v.Event.EventType == "SubscriptionCheckpoint")
						.Select(x => x.Event).FirstOrDefault();
					if (checkpoint != null) {
						long lastEvent = checkpoint.Data.ParseJson<long>();
						_onStateLoaded(lastEvent);
						return;
					}
				}

				_onStateLoaded(null);
			}
		}
	}
}
