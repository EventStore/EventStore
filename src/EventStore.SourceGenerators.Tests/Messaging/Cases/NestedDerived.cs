namespace EventStore.SourceGenerators.Tests.Messaging.NestedDerived {
	[DerivedMessage]
	partial class B : Message {
		[DerivedMessage]
		private partial class A : B {
		} 
	}
}
