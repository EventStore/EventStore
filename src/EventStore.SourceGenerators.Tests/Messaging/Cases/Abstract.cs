namespace EventStore.SourceGenerators.Tests.Messaging.Abstract {
	[DerivedMessage]
	abstract partial class B : Message {
		[DerivedMessage]
		private partial class A : B {
		}
	}
}
