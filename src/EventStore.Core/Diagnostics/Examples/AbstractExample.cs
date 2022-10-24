using EventStore.Core.Messaging;

namespace EventStore.Core.Diagnostics.Examples.Abstract {
	[StatsMessage]
	abstract partial class B : Message {
		[StatsGroup("nested-derived-example")]
		public enum MessageType { A, B, C, D, E }

		[StatsMessage(MessageType.A)]
		private partial class A : B {
		} 
	}
}
