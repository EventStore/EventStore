namespace EventStore.SourceGenerators.Tests.Messaging.Nested {
	public partial class N {
		[DerivedMessage]
		public partial class A : Message {
		}

		[DerivedMessage]
		public partial class B : Message {
		}
	}
}
