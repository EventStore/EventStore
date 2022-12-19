using System;

namespace EventStore.SourceGenerators.Tests.Messaging {
	[AttributeUsage(AttributeTargets.Class)]
	public class BaseMessageAttribute : Attribute {
		public BaseMessageAttribute() {
		}
	}

	[AttributeUsage(AttributeTargets.Class)]
	public class DerivedMessageAttribute : Attribute {
		public DerivedMessageAttribute() {
		}
	}

	[BaseMessage]
	public abstract partial class Message {
	}
}
