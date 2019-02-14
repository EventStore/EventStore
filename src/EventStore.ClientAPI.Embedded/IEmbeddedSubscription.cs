using System;
using System.Threading.Tasks;

namespace EventStore.ClientAPI.Embedded {
	internal interface IEmbeddedSubscription {
		void DropSubscription(EventStore.Core.Services.SubscriptionDropReason reason, Exception ex = null);
		void ConfirmSubscription(long lastCommitPosition, long? lastEventNumber);
		void Unsubscribe();
		void Start(Guid correlationId);
	}
}
