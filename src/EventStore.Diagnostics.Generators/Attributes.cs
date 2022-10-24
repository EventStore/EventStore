using System;

//qq not sure what this namespace should be called
// or where they should be. they need to be accessible from core, plugins that create messages
// and the source generators.

namespace EventStore {

	public class RootMessageAttribute : Attribute {
		public RootMessageAttribute() {
		}
	}

	public class StatsMessageAttribute : Attribute {
		// enumMember need not be provided for abstract messages
		public StatsMessageAttribute(object enumMember = null) {
		}
	}

	public class StatsGroupAttribute : Attribute {
		public StatsGroupAttribute(string uniqueName) {
		}
	}
}

//qqqq figure out where this should go
namespace EventStore {
	public static class Names {
		public const string EventSourceStatic = nameof(EventSourceStatic);
		public const string MessageTypeIdStatic = nameof(MessageTypeIdStatic);
		public const string StatsIdStatic = nameof(StatsIdStatic);
	}
}
