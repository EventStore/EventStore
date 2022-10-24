using EventStore.Core.Messaging;

namespace EventStore.Core.Diagnostics.Examples.NestedDerived {
	[StatsGroup("nested-derived-example")]
	public enum MessageType { A, B, C, D, E }

	[StatsMessage(MessageType.B)]
	partial class B : Message {
		[StatsMessage(MessageType.A)]
		private partial class A : B {
		} 
	}
}
