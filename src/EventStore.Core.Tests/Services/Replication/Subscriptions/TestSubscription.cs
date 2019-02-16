using System;
using System.Threading;
using System.Collections.Generic;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.UserManagement;

namespace EventStore.Core.Tests.Replication.ReadStream {
	public class TestSubscription {
		public MiniClusterNode Node;
		public CountdownEvent SubscriptionsConfirmed;
		public CountdownEvent EventAppeared;
		public List<ClientMessage.StreamEventAppeared> StreamEvents;
		public string StreamId;

		public TestSubscription(MiniClusterNode node, int expectedEvents, string streamId,
			CountdownEvent subscriptionsConfirmed) {
			Node = node;
			SubscriptionsConfirmed = subscriptionsConfirmed;
			EventAppeared = new CountdownEvent(expectedEvents);
			StreamId = streamId;
		}

		public void CreateSubscription() {
			var subscribeMsg = new ClientMessage.SubscribeToStream(Guid.NewGuid(), Guid.NewGuid(),
				new CallbackEnvelope(x => {
					switch (x.GetType().Name) {
						case "SubscriptionConfirmation":
							SubscriptionsConfirmed.Signal();
							break;
						case "StreamEventAppeared":
							EventAppeared.Signal();
							break;
						case "SubscriptionDropped":
							break;
						default:
							Assert.Fail("Unexpected message type :" + x.GetType().Name);
							break;
					}
				}), Guid.NewGuid(), StreamId, false, SystemAccount.Principal);
			Node.Node.MainQueue.Publish(subscribeMsg);
		}
	}
}
