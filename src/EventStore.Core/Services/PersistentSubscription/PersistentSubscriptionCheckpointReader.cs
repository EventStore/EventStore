// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable
using System;
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
					var checkpoint = msg.Events[0].Event;

					if (checkpoint.EventType == "SubscriptionCheckpoint" ||
						checkpoint.EventType == "$SubscriptionCheckpoint") {
				
						var checkpointJson = checkpoint.Data.ParseJson<string>();
						_onStateLoaded(checkpointJson);
						return;
					}
				}

				_onStateLoaded(null);
			}
		}
	}
}
