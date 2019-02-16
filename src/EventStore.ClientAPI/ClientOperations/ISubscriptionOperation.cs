using System;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Tcp;

namespace EventStore.ClientAPI.ClientOperations {
	internal interface ISubscriptionOperation {
		void DropSubscription(SubscriptionDropReason reason, Exception exc, TcpPackageConnection connection = null);
		void ConnectionClosed();
		InspectionResult InspectPackage(TcpPackage package);
		bool Subscribe(Guid correlationId, TcpPackageConnection connection);
	}
}
