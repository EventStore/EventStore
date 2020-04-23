using System.Linq;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.LeaderReplication {
	[TestFixture]
	public class when_replication_service_recieves_leader_log_commited_to : with_replication_service {
		private long _logPosition;
		
		public override void When() {			
			_logPosition = 4000;
			Service.Handle(new ReplicationTrackingMessage.ReplicatedTo(_logPosition));
		}

		[Test]
		public void replicated_to_should_be_sent_to_subscriptions() {
			AssertEx.IsOrBecomesTrue(() => TcpSends.Count > 4, msg:"TcpSend msg not recieved");
			var sends = TcpSends.Where(tcpSend => tcpSend.Message is ReplicationTrackingMessage.ReplicatedTo).ToList();
			Assert.AreEqual(3 * 2,sends.Count); //one ReplicatedTo message in sent when the subscription is added and the second one in When()
			Assert.AreEqual(2, sends.Count( msg=> msg.ConnectionManager.ConnectionId == ReplicaSubscriptionId));
			Assert.AreEqual(2, sends.Count( msg=> msg.ConnectionManager.ConnectionId == ReplicaSubscriptionId2));
			Assert.AreEqual(2, sends.Count( msg=> msg.ConnectionManager.ConnectionId == ReadOnlyReplicaSubscriptionId));
			
		}
	}
}
